using Content.Shared._Nibiru.Factions;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Shared.Random;
using Content.Shared.Database;
using Robust.Shared.Player;

namespace Content.Server._Nibiru.Factions;

public sealed partial class FactionSystem
{
    private void OnMobStateChanged(EntityUid uid, FactionComponent component, MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead || args.OldMobState >= args.NewMobState || !component.IsCreator)
            return;

        EntityUid newLeader = EntityUid.Invalid;

        if (TryComp<FactionComponent>(component.Heir, out var heirComponent)
        && heirComponent.FactionName == component.FactionName
        && component.Heir.Valid
        && TryComp<MobStateComponent>(component.Heir, out var heirMobStateComponent)
        && heirMobStateComponent.CurrentState == MobState.Alive)
        {
            // У нас уже есть живой наследник
            newLeader = component.Heir;
        }
        else
        {
            // Ищем подходящего наследника по списку сверху вниз
            foreach (var memberUid in component.Members)
            {
                if (!TryComp<MobStateComponent>(memberUid, out var ms) || ms.CurrentState != MobState.Alive)
                    continue;

                if (!TryComp<FactionComponent>(memberUid, out var memberComp) || memberComp.FactionName != component.FactionName)
                    continue;

                bool canInherit = false;
                var roleIndex = component.Roles.FindIndex(r => r.Name == memberComp.Rank);
                if (roleIndex >= 0 && component.Roles[roleIndex].CanInherit)
                    canInherit = true;

                if (canInherit)
                {
                    newLeader = memberUid;
                    break;
                }
            }

            // Если никто не подошел по рангу, берем просто первого живого по списку
            if (!newLeader.Valid)
            {
                foreach (var memberUid in component.Members)
                {
                    if (TryComp<MobStateComponent>(memberUid, out var ms) && ms.CurrentState == MobState.Alive &&
                        TryComp<FactionComponent>(memberUid, out var memberComp) && memberComp.FactionName == component.FactionName)
                    {
                        newLeader = memberUid;
                        break;
                    }
                }
            }
        }

        if (newLeader.Valid && TryComp<FactionComponent>(newLeader, out var newLeaderComp))
        {
            newLeaderComp.IsCreator = true;
            newLeaderComp.Members = component.Members;
            newLeaderComp.AllMembers = component.AllMembers;
            newLeaderComp.Members.Remove(newLeader);
            newLeaderComp.Roles = component.Roles;
            newLeaderComp.Rank = Loc.GetString("faction-rank-leader");
            component.IsCreator = false;
            
            _adminLog.Add(LogType.Action, LogImpact.High, $"{ToPrettyString(uid):player} умер. Новым лидером фракции {component.FactionName} стал {ToPrettyString(newLeader):player}");
        }
        else if (component.Members.Count > 0)
        {
            var randomMember = _random.Pick(component.Members);

            if (TryComp<FactionComponent>(randomMember, out var memberComp))
            {
                memberComp.IsCreator = true;
                memberComp.Members = component.Members;
                memberComp.AllMembers = component.AllMembers;
                memberComp.Members.Remove(randomMember);
                memberComp.Rank = Loc.GetString("faction-rank-leader");
                component.IsCreator = false;

                foreach (var member in memberComp.Members)
                {
                    if (TryComp<FactionComponent>(member, out var memComp))
                    {
                        memComp.Leader = randomMember;
                        Dirty(member, memComp);
                    }
                }

                Dirty(randomMember, memberComp);

                _adminLog.Add(LogType.Action, LogImpact.High, $"{ToPrettyString(uid):player} умер. Новым лидером фракции {component.FactionName} стал случайный участник {ToPrettyString(randomMember):player}");

                UpdateFactionRegistry(memberComp);
            }
        }
    }

    private void OnHeirChoose(HeirChooseMessage msg, EntitySessionEventArgs args)
    {
        var player = args.SenderSession.AttachedEntity;
        var heir = GetEntity(msg.Heir);

        if (!player.HasValue || heir == player.Value)
            return;

        if (!TryComp<FactionComponent>(player.Value, out var factionComponent)
            || !factionComponent.IsCreator
            || !TryComp<FactionComponent>(heir, out var heirComponent)
            || heirComponent.FactionName != factionComponent.FactionName)
        {
            _popup.PopupEntity(
                Loc.GetString("not-in-youre-faction"),
                player.Value,
                player.Value);
            return;
        }

        factionComponent.Heir = heir;
        Dirty(player.Value, factionComponent);

        _adminLog.Add(LogType.Action, LogImpact.High, $"{ToPrettyString(player.Value):player} назначил {ToPrettyString(heir):player} наследником во фракции {factionComponent.FactionName}");

        UpdateFactionRegistry(factionComponent);
    }

    private void OnTitleTransfer(FactionTitleTransferMessage msg, EntitySessionEventArgs args)
    {
        var player = args.SenderSession.AttachedEntity;
        var entity = GetEntity(msg.entity);

        if (!player.HasValue || entity == player.Value)
            return;

        if (!TryComp<FactionComponent>(player.Value, out var factionComponent)
            || !factionComponent.IsCreator
            || !TryComp<FactionComponent>(entity, out var entityComponent)
            || entityComponent.FactionName != factionComponent.FactionName)
        {
            _popup.PopupEntity(
                Loc.GetString("not-in-youre-faction"),
                player.Value,
                player.Value);
            return;
        }

        factionComponent.IsCreator = false;
        factionComponent.Leader = entity;
        var tempRank = factionComponent.Rank;
        factionComponent.Rank = entityComponent.Rank;

        entityComponent.Members = factionComponent.Members;
        entityComponent.AllMembers = factionComponent.AllMembers;
        entityComponent.Members.Remove(entity);
        entityComponent.Members.Add(player.Value);
        entityComponent.IsCreator = true;
        entityComponent.Rank = Loc.GetString("faction-rank-leader");
        entityComponent.Roles = factionComponent.Roles;

        foreach (var member in factionComponent.Members)
        {
            if (TryComp<FactionComponent>(member, out var memberComp))
            {
                memberComp.Leader = entity;
                Dirty(member, memberComp);
            }
        }

        Dirty(player.Value, factionComponent);
        Dirty(entity, entityComponent);

        _adminLog.Add(LogType.Action, LogImpact.High, $"{ToPrettyString(player.Value):player} передал лидерство {ToPrettyString(entity):player} во фракции {factionComponent.FactionName}");

        UpdateFactionRegistry(entityComponent);
    }

    private void OnDeleteFaction(FactionDeleteMessage msg, EntitySessionEventArgs args)
    {
        var player = args.SenderSession.AttachedEntity;

        if (!player.HasValue)
            return;

        if (!TryComp<FactionComponent>(player.Value, out var factionComponent)
            || !factionComponent.IsCreator)
        {
            _popup.PopupEntity(
                Loc.GetString("not-leader"),
                player.Value,
                player.Value);
            return;
        }

        var factionName = factionComponent.FactionName;
        EntityManager.System<Content.Server.Research.Systems.ResearchSystem>().ClearFactionResearch(factionName);

        var toStrip = new List<EntityUid>();
        var query = EntityQueryEnumerator<FactionComponent>();
        while (query.MoveNext(out var uid, out var faction))
        {
            if (faction.FactionName == factionName)
                toStrip.Add(uid);
        }

        foreach (var uid in toStrip)
        {
            if (TryComp<ActorComponent>(uid, out _))
            {
                _popup.PopupEntity(
                    Loc.GetString("faction-disbanded", ("factionName", factionName)),
                    uid,
                    uid);
            }

            RemComp<FactionComponent>(uid);
        }

        _adminLog.Add(LogType.Action, LogImpact.High, $"{ToPrettyString(player.Value):player} распустил фракцию {factionName}");

        UnregisterFaction(factionName);
    }
}

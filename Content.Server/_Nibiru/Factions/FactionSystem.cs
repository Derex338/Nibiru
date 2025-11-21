using Content.Shared._Nibiru.Factions;
using Robust.Shared.Prototypes;
using Content.Server.Popups;
using Content.Shared.Construction.Prototypes;
using System.Linq;
using Content.Server.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.Mobs;
using Robust.Shared.Random;
using Content.Shared.Mobs.Components;

namespace Content.Server._Nibiru.Factions;

public sealed class FactionSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly IAdminLogManager _adminLog = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private static readonly HashSet<Entity<FactionComponent>> ClientLookup = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<FactionCreateRequestMessage>(OnFactionCreateRequest);
        SubscribeNetworkEvent<FactionStateRequestMessage>(OnFactionStateRequest);

        SubscribeNetworkEvent<HeirChooseMessage>(OnHeirChoose);
        SubscribeNetworkEvent<FactionTitleTransferMessage>(OnTitleTransfer);
        SubscribeNetworkEvent<FactionLeaveMessage>(OnLeaveFaction);
        SubscribeNetworkEvent<FactionDeleteMessage>(OnDeleteFaction);
        SubscribeNetworkEvent<FactionKickMemberMessage>(OnKickMemberFaction);

        SubscribeNetworkEvent<FactionChangeStateMessage>(OnFactionStateChange);

        SubscribeLocalEvent<FactionComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnFactionCreateRequest(FactionCreateRequestMessage msg, EntitySessionEventArgs args)
    {
        // Проверяем, что имя фракции корректное
        if (string.IsNullOrWhiteSpace(msg.FactionName))
        {
            return;
        }

        var player = args.SenderSession.AttachedEntity;

        if (!player.HasValue)
            return;

        var allFactions = GetFactions(player.Value).ToList();

        foreach (var faction in allFactions)
        {
            if (EntityManager.TryGetComponent<FactionComponent>(faction, out var factionComponent)
                && factionComponent.FactionName == msg.FactionName)
            {
                _popup.PopupEntity(
                Loc.GetString("faction-already-exist", ("factionName", factionComponent.FactionName)),
                player.Value,
                player.Value);
                return;
            }
        }

        // Создаём фракцию
        CreateFaction(player.Value, msg.FactionName);
    }

    private void CreateFaction(EntityUid player, string factionName)
    {
        // Выдаём компонент фракции
        if (!EntityManager.TryGetComponent<FactionComponent>(player, out var factionComponent))
        {
            factionComponent = EntityManager.AddComponent<FactionComponent>(player);

            factionComponent.FactionName = factionName;
            factionComponent.IsCreator = true;

            _adminLog.Add(LogType.FactionCreated, LogImpact.Medium, $"{ToPrettyString(player):player} создал фракцию с названием {factionName}");

            foreach (var recipe in _prototypeManager.EnumeratePrototypes<ConstructionPackPrototype>())
            {
                factionComponent.StaticPacks.Add(recipe.ID);
            }

            Dirty(player, factionComponent);
        }
    }

    private void OnFactionStateRequest(FactionStateRequestMessage msg, EntitySessionEventArgs args)
    {
        var player = args.SenderSession.AttachedEntity;

        if (!player.HasValue)
            return;

        if (EntityManager.TryGetComponent<FactionComponent>(player, out var factionComponent))
        {
            if (factionComponent.IsCreator == true)
                msg.Creator = true;

            msg.FactionName = factionComponent.FactionName;
        }


    }

    public HashSet<Entity<FactionComponent>> GetFactions(EntityUid client)
    {
        ClientLookup.Clear();

        var clientXform = Transform(client);

        _lookup.GetEntitiesOnMap(clientXform.MapID, ClientLookup);
        return ClientLookup;
    }

    private void OnMobStateChanged(EntityUid uid, FactionComponent component, MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead || args.OldMobState >= args.NewMobState || component.IsCreator)
            return;

        if (TryComp<FactionComponent>(component.Heir, out var heir)
        && heir.FactionName == component.FactionName
        && component.Heir.Valid
        && TryComp<MobStateComponent>(component.Heir, out var mobStateComponent)
        && mobStateComponent.CurrentState == MobState.Alive)
        {
            heir.IsCreator = true;
            heir.Members = component.Members;
            heir.Members.Remove(component.Heir);
            component.IsCreator = false;

            foreach (var member in heir.Members)
            {
                if (TryComp<FactionComponent>(member, out var memberComp))
                {
                    memberComp.Leader = component.Heir;
                    Dirty(member, memberComp);
                }
            }
            Dirty(component.Heir, heir);
        }
        else if (component.Members.Count > 0)
        {
            var randomMember = _random.Pick(component.Members);

            if (TryComp<FactionComponent>(randomMember, out var memberComp))
            {
                memberComp.IsCreator = true;
                memberComp.Members = component.Members;
                memberComp.Members.Remove(randomMember);
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
        else
        {
            factionComponent.Heir = heir;
            Dirty(player.Value, factionComponent);
        }
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
        else
        {
            factionComponent.IsCreator = false;
            factionComponent.Leader = entity;
            entityComponent.Members = factionComponent.Members;
            entityComponent.Members.Remove(entity);
            entityComponent.Members.Add(player.Value);
            entityComponent.IsCreator = true;

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
        }
    }

    private void OnLeaveFaction(FactionLeaveMessage msg, EntitySessionEventArgs args)
    {
        var player = args.SenderSession.AttachedEntity;

        if (!player.HasValue)
            return;

        if (!TryComp<FactionComponent>(player.Value, out var factionComponent)
            || factionComponent.IsCreator)
        {
            _popup.PopupEntity(
                Loc.GetString("no-faction-to-leave"),
                player.Value,
                player.Value);
            return;
        }
        else
        {
            if (!TryComp<FactionComponent>(factionComponent.Leader, out var leaderComponent))
                return;

            leaderComponent.Members.Remove(player.Value);
            Dirty(factionComponent.Leader, leaderComponent);

            RemComp<FactionComponent>(player.Value);
            Dirty(player.Value, factionComponent);
        }
    }

    private void OnKickMemberFaction(FactionKickMemberMessage msg, EntitySessionEventArgs args)
    {
        var player = args.SenderSession.AttachedEntity;
        var member = GetEntity(msg.Member);

        if (!player.HasValue)
            return;

        if (!TryComp<FactionComponent>(player.Value, out var factionComponent)
            || !TryComp<FactionComponent>(member, out var memberComponent)
            || memberComponent.FactionName != factionComponent.FactionName
            || !factionComponent.IsCreator)
        {
            _popup.PopupEntity(
                Loc.GetString("cant-kick-member"),
                player.Value,
                player.Value);
            return;
        }
        else
        {
            factionComponent.Members.Remove(member);
            Dirty(player.Value, factionComponent);

            _popup.PopupEntity(
                Loc.GetString("faction-kicked", ("factionName", factionComponent.FactionName)),
                member,
                member);

            RemComp<FactionComponent>(member);
            Dirty(member, memberComponent);
        }
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
        else
        {
            foreach (var member in factionComponent.Members)
            {
                if (TryComp<FactionComponent>(member, out var memberComp))
                {
                    _popup.PopupEntity(
                        Loc.GetString("faction-disbanded", ("factionName", factionComponent.FactionName)),
                        member,
                        member);

                    RemComp<FactionComponent>(member);
                    Dirty(member, memberComp);
                }
            }

            RemComp<FactionComponent>(player.Value);

            Dirty(player.Value, factionComponent);
        }
    }

    private void OnFactionStateChange(FactionChangeStateMessage msg, EntitySessionEventArgs args)
    {
        var player = args.SenderSession.AttachedEntity;

        if (!player.HasValue)
            return;

        var allFactions = GetFactions(player.Value).ToList();
        bool factionNameAvaliable = true;

        foreach (var faction in allFactions)
        {
            if (TryComp<FactionComponent>(faction, out var factionComp)
                && factionComp.FactionName == msg.FactionName)
            {
                _popup.PopupEntity(
                Loc.GetString("faction-already-exist", ("factionName", factionComp.FactionName)),
                player.Value,
                player.Value);

                factionNameAvaliable = false;

                break;
            }
        }

        if (!TryComp<FactionComponent>(player.Value, out var factionComponent)
            || !factionComponent.IsCreator)
        {
            _popup.PopupEntity(
                Loc.GetString("not-leader"),
                player.Value,
                player.Value);
            return;
        }
        else if (factionComponent.Members.Count > 0)
        {
            foreach (var member in factionComponent.Members)
            {
                if (TryComp<FactionComponent>(member, out var memberComp))
                {
                    if (msg.FactionName != null && factionNameAvaliable)
                    {
                        _popup.PopupEntity(
                        Loc.GetString("faction-name-changed", ("factionName", msg.FactionName)),
                        member,
                        member);

                        memberComp.FactionName = msg.FactionName;
                        factionComponent.FactionName = msg.FactionName;
                    }

                    if (msg.Color != null)
                    {
                        factionComponent.FactionColor = msg.Color.Value;
                        memberComp.FactionColor = msg.Color.Value;
                    }

                    Dirty(player.Value, factionComponent);
                    Dirty(member, memberComp);
                }
            }
        }
        else
        {
            if (msg.FactionName != null && factionNameAvaliable)
            {
                factionComponent.FactionName = msg.FactionName;
            }
            if (msg.Color != null)
            {
                factionComponent.FactionColor = msg.Color.Value;
            }
            Dirty(player.Value, factionComponent);
        }
    }
}

using Content.Shared._Nibiru.Factions;
using Robust.Shared.Map;
using Robust.Shared.Map;
using System.Linq;
using Content.Shared.Database;

namespace Content.Server._Nibiru.Factions;

public sealed partial class FactionSystem
{
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

        if (!TryComp<FactionComponent>(factionComponent.Leader, out var leaderComponent))
            return;

        leaderComponent.Members.Remove(player.Value);
        Dirty(factionComponent.Leader, leaderComponent);

        UpdateFactionRegistry(leaderComponent);

        RemComp<FactionComponent>(player.Value);
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

        factionComponent.Members.Remove(member);
        Dirty(player.Value, factionComponent);

        UpdateFactionRegistry(factionComponent);

        _adminLog.Add(LogType.Action, LogImpact.High, $"{ToPrettyString(player.Value):player} кикнул {ToPrettyString(member):player} из фракции {factionComponent.FactionName}");

        _popup.PopupEntity(
            Loc.GetString("faction-kicked", ("factionName", factionComponent.FactionName)),
            member,
            member);

        RemComp<FactionComponent>(member);
    }

    private void OnChangeMemberRank(FactionChangeMemberRankMessage msg, EntitySessionEventArgs args)
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
                Loc.GetString("cant-change-rank"),
                player.Value,
                player.Value);
            return;
        }

        memberComponent.Rank = msg.NewRank;
        Dirty(member, memberComponent);

        if (TryComp<FactionComponent>(factionComponent.Leader, out var leaderComp))
        {
             UpdateMemberDataUI(factionComponent.Leader, leaderComp);
        }

        _adminLog.Add(LogType.Action, LogImpact.Medium, $"{ToPrettyString(player.Value):player} изменил ранг {ToPrettyString(member):player} на {msg.NewRank} во фракции {factionComponent.FactionName}");

        _popup.PopupEntity(
            Loc.GetString("rank-changed", ("rank", msg.NewRank)),
            member,
            member);
    }

    private void OnMoveMember(FactionMoveMemberMessage msg, EntitySessionEventArgs args)
    {
        var player = args.SenderSession.AttachedEntity;
        var member = GetEntity(msg.Member);

        if (!player.HasValue)
            return;

        if (!TryComp<FactionComponent>(player.Value, out var factionComponent)
            || !factionComponent.IsCreator)
            return;

        var index = factionComponent.Members.IndexOf(member);
        if (index == -1)
            return;

        if (msg.MoveUp && index > 0)
        {
            factionComponent.Members.RemoveAt(index);
            factionComponent.Members.Insert(index - 1, member);
        }
        else if (!msg.MoveUp && index < factionComponent.Members.Count - 1)
        {
            factionComponent.Members.RemoveAt(index);
            factionComponent.Members.Insert(index + 1, member);
        }
        else
        {
            return;
        }

        UpdateMemberDataUI(player.Value, factionComponent);
        UpdateFactionRegistry(factionComponent);
    }
}

using Robust.Shared.Player;
using Content.Shared.Examine;
using Content.Shared.IdentityManagement;
using Content.Shared.IdentityManagement.Components;

namespace Content.Shared._Nibiru.Factions;

public sealed partial class SharedFactionSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FactionComponent, ExaminedEvent>(OnExamined);
    }

    private void OnExamined(EntityUid uid, FactionComponent component, ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        var ev = new SeeIdentityAttemptEvent();
        RaiseLocalEvent(uid, ev);
        if (ev.Cancelled)
            return;

        var colorHex = component.FactionColor.ToHex();
        var rank = string.IsNullOrEmpty(component.Rank) ? Loc.GetString("faction-rank-no-rank") : component.Rank;

        args.PushMarkup(Loc.GetString("faction-examine-member", ("color", colorHex), ("name", component.FactionName), ("rank", rank)));
    }

    public bool OnFactionStateRequest(ICommonSession session, bool CreatorCheck)
    {
        var player = session.AttachedEntity;

        if (!player.HasValue)
            return false;

        if (TryComp<FactionComponent>(player, out var factionComponent))
        {
            if (CreatorCheck)
                return factionComponent.IsCreator;

            return !factionComponent.IsCreator;
        }

        return false;
    }
}

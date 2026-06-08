using Robust.Shared.Player;
using Content.Shared.Examine;
using Content.Shared.IdentityManagement;
using Content.Shared.IdentityManagement.Components;

namespace Content.Shared._Nibiru.Factions;

public sealed class SharedFactionSystem : EntitySystem
{
    [Dependency] private readonly IEntityManager _entity = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FactionComponent, ExaminedEvent>(OnExamined);
    }

    private void OnExamined(EntityUid uid, FactionComponent component, ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        // Do not show faction if identity is hidden
        var ev = new SeeIdentityAttemptEvent();
        _entity.EventBus.RaiseLocalEvent(uid, ev);
        if (ev.Cancelled)
            return;

        var colorHex = component.FactionColor.ToHex();
        var rank = string.IsNullOrEmpty(component.Rank) ? Loc.GetString("faction-rank-no-rank") : component.Rank;
        
        args.PushMarkup($"\nУчастник фракции [color={colorHex}]{component.FactionName}[/color]. Роль: {rank}.");
    }

    public bool OnFactionStateRequest(ICommonSession session, bool CreatorCheck)
    {
        var player = session.AttachedEntity;

        if (!player.HasValue)
            return false;

        if (EntityManager.TryGetComponent<FactionComponent>(player, out var factionComponent))
        {
            if(CreatorCheck)
                return factionComponent.IsCreator;
            else if (!CreatorCheck)
                return !factionComponent.IsCreator;
        }

        return false;
    }
}

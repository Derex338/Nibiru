using Content.Server.Antag.Components;
using Content.Shared.GameTicking.Components;
using Content.Server.GameTicking.Rules;

namespace Content.Server.Antag;

public sealed partial class AntagRandomSpawnSystem : GameRuleSystem<AntagRandomSpawnComponent>
{
    private const int TilePickAttempts = 32;

    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AntagRandomSpawnComponent, AntagSelectLocationEvent>(OnSelectLocation);
    }

    protected override void Added(EntityUid uid, AntagRandomSpawnComponent comp, GameRuleComponent gameRule, GameRuleAddedEvent args)
    {
        base.Added(uid, comp, gameRule, args);

        // we have to select this here because AntagSelectLocationEvent is raised twice because MakeAntag is called twice
        // once when a ghost role spawner is created and once when someone takes the ghost role

        TryPickCoords(comp);
    }

    private void OnSelectLocation(Entity<AntagRandomSpawnComponent> ent, ref AntagSelectLocationEvent args)
    {
        if (ent.Comp.Coords == null)
            TryPickCoords(ent.Comp);

        if (ent.Comp.Coords != null)
            args.Coordinates.Add(_transform.ToMapCoordinates(ent.Comp.Coords.Value));
    }

    private void TryPickCoords(AntagRandomSpawnComponent comp)
    {
        for (var attempt = 0; attempt < TilePickAttempts; attempt++)
        {
            if (TryFindRandomTile(out _, out _, out _, out var coords))
            {
                comp.Coords = coords;
                return;
            }
        }
    }
}

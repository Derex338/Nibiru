using Content.Shared.Maps;
using Content.Shared.Random.Rules;
using Robust.Shared.Prototypes;
using Robust.Shared.Map;
using Robust.Shared.IoC;
using Microsoft.CodeAnalysis;

namespace Content.Shared._Nibiru;

public sealed partial class StayOnTileRule : RulesRule
{
    [DataField(required: true)]
    public List<string> tiles = default!;

    public override bool Check(EntityManager entManager, EntityUid uid)
    {
        if (!entManager.TryGetComponent<TransformComponent>(uid, out var comp))
            return false;

        if (!IoCManager.Resolve<IEntityManager>().TrySystem<TurfSystem>(out var turfSystem))
            return false;

        if (!turfSystem.TryGetTileRef(comp.Coordinates, out var tileFound))
            return false;

        var tile = turfSystem.GetContentTileDefinition(tileFound.Value);
        foreach (var targetTile in tiles)
        {
            if (tile.ID == targetTile)
                return true;
        }
        return false;
    }
}

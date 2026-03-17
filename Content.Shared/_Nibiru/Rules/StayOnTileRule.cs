using Content.Shared.Parallax.Biomes;
using Content.Shared.Random.Rules;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Shared._Nibiru;

/// <summary>
///     Checks if the player is standing on a specific biome.
/// </summary>
public sealed partial class StayOnTileRule : RulesRule
{
    [DataField(required: true)]
    public string biomeId = string.Empty;

    public override bool Check(EntityManager entManager, EntityUid uid)
    {
        if (!entManager.TryGetComponent<TransformComponent>(uid, out var xform))
            return false;

        if (xform.GridUid == null)
            return false;

        if (!entManager.TryGetComponent<BiomeComponent>(xform.GridUid.Value, out var biomeComp) ||
            !entManager.TryGetComponent<MapGridComponent>(xform.GridUid.Value, out var gridComp))
            return false;

        var biomeSystem = entManager.System<SharedBiomeSystem>();
        var mapSystem = entManager.System<SharedMapSystem>();
        var indices = mapSystem.TileIndicesFor(xform.GridUid.Value, gridComp, xform.Coordinates);

        if (!biomeSystem.TryGetBiomeTemplate(xform.GridUid.Value, biomeComp, indices, out var templateId))
            return false;

        return templateId == biomeId;
    }
}

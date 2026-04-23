using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Content.Shared.Construction;
using Content.Shared.Construction.Conditions;
using Content.Shared.Maps;
using Content.Shared.Physics;
using JetBrains.Annotations;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Shared._Nibiru.Construction;

[UsedImplicitly]
[DataDefinition]
public sealed partial class MultiLevelTileCondition : IConstructionCondition
{
    [DataField("offsets")]
    public List<int> Offsets = new();

    [DataField("requireSpace")]
    public bool RequireSpace = true;

    [DataField("requireNotBlocked")]
    public bool RequireNotBlocked = true;

    [DataField("useRotation")]
    public bool UseRotation = false;

    public bool Condition(EntityUid user, EntityCoordinates location, Direction direction)
    {
        var entManager = IoCManager.Resolve<IEntityManager>();
        if (!entManager.TrySystem<CESharedZLevelsSystem>(out var zLevelsSystem))
            return false;
        if (!entManager.TrySystem<TurfSystem>(out var turfSystem))
            return false;
        if (!entManager.TrySystem<SharedTransformSystem>(out var transformSystem))
            return false;
        if (!entManager.TrySystem<SharedMapSystem>(out var mapSystem))
            return false;

        var currentMap = transformSystem.GetMap(location.EntityId);
        if (currentMap == null || !entManager.TryGetComponent<CEZLevelMapComponent>(currentMap, out var currentZMap))
            return false;

        var worldPos = transformSystem.ToMapCoordinates(location);
        var targetPos = worldPos.Position;
        if (UseRotation)
        {
            targetPos += direction.ToVec();
        }

        // 1. Check current level at the target horizontal position.
        // For a rope, the tile in front of the peg MUST be space/hole.
        if (RequireSpace)
        {
            var currentTargetCoords = new MapCoordinates(targetPos, worldPos.MapId);
            if (IoCManager.Resolve<IMapManager>().TryFindGridAt(currentTargetCoords, out var currentGridUid, out var currentGrid))
            {
                var indices = mapSystem.WorldToTile(currentGridUid, currentGrid, targetPos);
                var tileRef = mapSystem.GetTileRef(currentGridUid, currentGrid, indices);
                if (!tileRef.Tile.IsEmpty && !turfSystem.IsSpace(tileRef))
                    return false;
            }
        }

        // 2. Check other levels.
        foreach (var offset in Offsets)
        {
            if (!zLevelsSystem.TryMapOffset((currentMap.Value, currentZMap), offset, out var targetMap))
                return false;

            var targetMapId = entManager.GetComponent<MapComponent>(targetMap.Value.Owner).MapId;
            var targetMapCoords = new MapCoordinates(targetPos, targetMapId);

            if (!IoCManager.Resolve<IMapManager>().TryFindGridAt(targetMapCoords, out var targetGridUid, out var targetGrid))
            {
                continue;
            }

            if (entManager.GetComponent<TransformComponent>(targetGridUid).MapID != targetMapId)
                continue;

            var indices = mapSystem.WorldToTile(targetGridUid, targetGrid, targetPos);
            var tileRef = mapSystem.GetTileRef(targetGridUid, targetGrid, indices);

            if (RequireNotBlocked)
            {
                if (turfSystem.IsTileBlocked(tileRef, CollisionGroup.Impassable))
                    return false;
            }
        }

        return true;
    }

    public ConstructionGuideEntry? GenerateGuideEntry()
    {
        return new ConstructionGuideEntry
        {
            Localization = "construction-step-condition-multilevel-tile-available",
        };
    }
}

using System.Numerics;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Content.Shared._Nibiru.Construction;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._Nibiru.Construcion;

public sealed partial class MultiLevelConstructionSystem : EntitySystem
{
[Dependency] private CESharedZLevelsSystem _zLevels = default!;
[Dependency] private SharedTransformSystem _transform = default!;
[Dependency] private TurfSystem _turf = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MultiLevelConstructionComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<MultiLevelConstructionComponent, EntityTerminatingEvent>(OnTerminating);
    }

    private void OnMapInit(EntityUid uid, MultiLevelConstructionComponent component, MapInitEvent args)
    {
        if (!component.IsOrigin || component.Projections.Count == 0)
            return;

        foreach (var projection in component.Projections)
        {
            ProjectToOffset(uid, projection.Prototype, projection.Offset, projection.LocalOffset, component.OffsetByRotation);
        }
    }

    private void OnTerminating(EntityUid uid, MultiLevelConstructionComponent component, ref EntityTerminatingEvent args)
    {
        if (component.LinkedEntities.Count == 0)
            return;

        var linked = new HashSet<EntityUid>(component.LinkedEntities);
        component.LinkedEntities.Clear();

        foreach (var other in linked)
        {
            if (other == uid || TerminatingOrDeleted(other))
                continue;

            if (TryComp<MultiLevelConstructionComponent>(other, out var otherComp))
            {
                otherComp.LinkedEntities.Remove(uid);
            }

            QueueDel(other);
        }
    }

    public EntityUid? ProjectToOffset(EntityUid origin, string prototype, int offset, Vector2 localOffset, bool offsetByRotation)
    {
        if (!TryComp(origin, out TransformComponent? xform))
            return null;

        var mapUid = xform.MapUid;
        if (mapUid == null || !TryComp<CEZLevelMapComponent>(mapUid, out var zMap))
            return null;

        if (!_zLevels.TryMapOffset((mapUid.Value, zMap), offset, out var targetMap))
            return null;

        var worldPos = _transform.GetWorldPosition(xform);
        var dirVec = Vector2.Zero;

        if (offsetByRotation)
        {
            var rotation = xform.LocalRotation;
            dirVec = rotation.GetCardinalDir().ToVec();
        }

        var targetPos = worldPos + dirVec + _transform.GetWorldRotation(origin).RotateVec(localOffset);

        var targetMapId = Comp<MapComponent>(targetMap.Value.Owner).MapId;
        var targetMapCoords = new MapCoordinates(targetPos, targetMapId);

        // 1. Validation on current map: is it Space?
        if (!IsLocationSpace(new MapCoordinates(targetPos, xform.MapID)))
            return null;

        // 2. Validation on target map: is it Blocked?
        if (IsLocationBlocked(targetMapCoords))
            return null;

        var spawned = Spawn(prototype, targetMapCoords);
        if (TerminatingOrDeleted(spawned))
            return null;

        var originComp = EnsureComp<MultiLevelConstructionComponent>(origin);
        var spawnedComp = EnsureComp<MultiLevelConstructionComponent>(spawned);

        originComp.LinkedEntities.Add(spawned);
        spawnedComp.LinkedEntities.Add(origin);

        foreach (var existing in originComp.LinkedEntities)
        {
            if (existing == spawned) continue;
            spawnedComp.LinkedEntities.Add(existing);
            if (TryComp<MultiLevelConstructionComponent>(existing, out var existingComp))
            {
                existingComp.LinkedEntities.Add(spawned);
            }
        }

        return spawned;
    }

    private bool IsLocationSpace(MapCoordinates coords)
    {
        if (!IoCManager.Resolve<IMapManager>().TryFindGridAt(coords, out var gridUid, out var grid))
            return true;

        if (!EntityManager.TrySystem<SharedMapSystem>(out var mapSystem))
            return false;

        var indices = mapSystem.WorldToTile(gridUid, grid, coords.Position);
        var tileRef = mapSystem.GetTileRef(gridUid, grid, indices);

        return tileRef.Tile.IsEmpty || _turf.IsSpace(tileRef);
    }

    private bool IsLocationBlocked(MapCoordinates coords)
    {
        if (!IoCManager.Resolve<IMapManager>().TryFindGridAt(coords, out var gridUid, out var grid))
            return false;

        if (!EntityManager.TrySystem<SharedMapSystem>(out var mapSystem))
            return false;

        var indices = mapSystem.WorldToTile(gridUid, grid, coords.Position);
        var tileRef = mapSystem.GetTileRef(gridUid, grid, indices);

        return _turf.IsTileBlocked(tileRef, CollisionGroup.Impassable);
    }
}

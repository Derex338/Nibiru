using Content.Server._CE.ZLevels.Core;
using System.Linq;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Content.Shared._CE.ZLevels.Light.Components;
using Content.Shared._CE.ZLevels.Roof;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Content.Shared.Maps;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using System.Numerics;

namespace Content.Server._CE.ZLevels.Light.EntitySystems;

/// <summary>
///     Manages the spawning of SunLightRayCast entities based on Z-level holes.
/// </summary>
public sealed class CEZLevelDaylightSystem : EntitySystem
{
    [Dependency] private readonly CESharedZLevelsSystem _zLevel = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDefMan = default!;
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly SharedRoofSystem _roofSystem = default!;

    private EntityQuery<MapGridComponent> _gridQuery;
    private EntityQuery<RoofComponent> _roofQuery;
    private EntityQuery<CEZLevelMapComponent> _zMapQuery;
    private EntityQuery<SunShadowComponent> _shadowQuery;

    public override void Initialize()
    {
        base.Initialize();

        _gridQuery = GetEntityQuery<MapGridComponent>();
        _roofQuery = GetEntityQuery<RoofComponent>();
        _zMapQuery = GetEntityQuery<CEZLevelMapComponent>();
        _shadowQuery = GetEntityQuery<SunShadowComponent>();

        SubscribeLocalEvent<TileChangedEvent>(OnTileChangedBroadcast, after: new[] { typeof(CESharedRoofSystem) });
        SubscribeLocalEvent<CEZLevelNetworkUpdatedEvent>(OnNetworkUpdatedBroadcast);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Sync sun direction across all maps in all networks
        var networkQuery = EntityQueryEnumerator<CEZLevelsNetworkComponent>();
        while (networkQuery.MoveNext(out var uid, out var network))
        {
            SyncNetworkSun(network);
        }
    }

    private void SyncNetworkSun(CEZLevelsNetworkComponent network)
    {
        // Find the top-most map with a sun shadow component
        EntityUid? topMap = null;
        SunShadowComponent? topShadow = null;

        foreach (var mapUid in network.ZLevels.OrderByDescending(kv => kv.Key).Select(kv => kv.Value))
        {
            if (mapUid == null) continue;
            if (_shadowQuery.TryComp(mapUid.Value, out var shadow))
            {
                topMap = mapUid.Value;
                topShadow = shadow;
                break;
            }
        }

        if (topMap == null || topShadow == null)
            return;

        // Propagate sun direction to all other maps in the network
        foreach (var mapUid in network.ZLevels.Values)
        {
            if (mapUid == null || mapUid == topMap) continue;
            if (_shadowQuery.TryComp(mapUid.Value, out var shadow))
            {
                if (shadow.Direction != topShadow.Direction || shadow.Alpha != topShadow.Alpha)
                {
                    shadow.Direction = topShadow.Direction;
                    shadow.Alpha = topShadow.Alpha;
                    Dirty(mapUid.Value, shadow);
                }
            }
        }
    }

    private void OnTileChangedBroadcast(ref TileChangedEvent args)
    {
        if (TryComp<CEZLevelMapRoofComponent>(args.Entity, out var roofMap))
            OnTileChanged((args.Entity, roofMap), ref args);
    }

    private void OnNetworkUpdatedBroadcast(CEZLevelNetworkUpdatedEvent args)
    {
        // Since we don't get the entity in the broadcast handler for this specific event type,
        // and network updates are rare, we refresh all networks.
        var query = EntityQueryEnumerator<CEZLevelsNetworkComponent>();
        while (query.MoveNext(out var uid, out var network))
        {
            foreach (var mapUid in network.ZLevels.Values)
            {
                if (mapUid.HasValue)
                    UpdateMapLight(mapUid.Value);
            }
        }
    }

    private void OnTileChanged(Entity<CEZLevelMapRoofComponent> ent, ref TileChangedEvent args)
    {
        if (!_gridQuery.TryComp(ent, out var grid))
            return;

        foreach (var change in args.Changes)
        {
            UpdateTileRay(ent.Owner, change.GridIndices);
        }

        if (_zMapQuery.TryComp(ent, out var zLevelMap))
        {
            var mapsBelow = _zLevel.GetAllMapsBelow((ent, zLevelMap));
            foreach (var mapBelow in mapsBelow)
            {
                foreach (var change in args.Changes)
                {
                    UpdateTileRay(mapBelow, change.GridIndices);
                }
            }
        }
    }

    public void UpdateMapLight(EntityUid mapUid)
    {
        if (!_gridQuery.TryComp(mapUid, out var grid))
            return;

        if (!_roofQuery.TryComp(mapUid, out var roof))
            return;

        if (_zMapQuery.TryComp(mapUid, out var zMap) && _zLevel.TryMapUp((mapUid, zMap), out _))
        {
            EnsureComp<SunLightRayComponent>(mapUid);
        }

        if (!HasComp<SunLightRayComponent>(mapUid))
            return;

        var enumerator = _map.GetAllTilesEnumerator(mapUid, grid);
        while (enumerator.MoveNext(out var tileRef))
        {
            UpdateTileRay(mapUid, tileRef.Value.GridIndices);
        }
    }

    private void UpdateTileRay(EntityUid mapUid, Vector2i indices)
    {
        if (!_gridQuery.TryComp(mapUid, out var grid))
            return;
        
        if (!_roofQuery.TryComp(mapUid, out var roof))
            return;

        var generator = EnsureComp<SunLightRayGeneratorComponent>(mapUid);

        bool shouldHaveRay = false;

        // 1. Is it rooved? (roof = there is a solid tile on the level above)
        if (!_roofSystem.IsRooved((mapUid, grid, roof), indices))
        {
            // 2. Is there a map ABOVE us in the network that provides light?
            if (_zMapQuery.TryComp(mapUid, out var zMap) && _zLevel.TryMapUp((mapUid, zMap), out _))
            {
                shouldHaveRay = true;
            }
        }

        bool hasRay = generator.Rays.ContainsKey(indices);

        if (shouldHaveRay && !hasRay)
        {
            var coords = new EntityCoordinates(mapUid, (Vector2)indices + new Vector2(0.5f, 0.5f));
            var ray = Spawn(null, coords);
            _entManager.AddComponent<SunLightRayCastComponent>(ray);
            generator.Rays[indices] = ray;
        }
        else if (!shouldHaveRay && hasRay)
        {
            if (generator.Rays.TryGetValue(indices, out var ray))
            {
                if (Exists(ray))
                    QueueDel(ray);
                generator.Rays.Remove(indices);
            }
        }
    }
}

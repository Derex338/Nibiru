using Content.Server._CE.ZLevels.Core;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._CE.ZLevels.Stability.Components;
using Content.Shared.Maps;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Maths;
using System.Linq;
using System.Collections.Generic;
using System.Numerics;

namespace Content.Server._CE.ZLevels.Stability;

public sealed partial class CEZLevelStabilitySystem : EntitySystem
{
    [Dependency] private CEZLevelsSystem _zLevels = default!;
    [Dependency] private SharedMapSystem _mapSystem = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private ITileDefinitionManager _tileDefManager = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private ILogManager _logManager = default!;

    private ISawmill _sawmill = default!;

    private const int MaxStabilitySearchRange = 10;

    private static readonly Vector2i[] Neighbors =
    {
        new(0, 1),
        new(0, -1),
        new(1, 0),
        new(-1, 0)
    };

    private bool _isProcessing = false;
    private readonly Queue<(EntityUid Grid, MapGridComponent Component, Vector2i Indices, EntityUid? Ignore)> _queuedChecks = new();
    private readonly HashSet<(EntityUid Grid, Vector2i Indices)> _activeChecks = new();

    public override void Initialize()
    {
        base.Initialize();
        _sawmill = _logManager.GetSawmill("zlevels.stability");

        SubscribeLocalEvent<TileChangedEvent>(OnTileChanged);
        SubscribeLocalEvent<ZLevelSupportComponent, ComponentStartup>(OnSupportStartup);
        SubscribeLocalEvent<ZLevelSupportComponent, ComponentShutdown>(OnSupportShutdown);
        SubscribeLocalEvent<ZLevelSupportComponent, AnchorStateChangedEvent>(OnAnchorChanged);
    }

    private void OnTileChanged(ref TileChangedEvent ev)
    {
        if (!TryComp<MapGridComponent>(ev.Entity, out var grid))
            return;

        foreach (var change in ev.Changes)
        {
            EnqueueCheck(ev.Entity, grid, change.GridIndices);
        }

        ProcessQueue();
    }

    private void OnSupportStartup(EntityUid uid, ZLevelSupportComponent component, ComponentStartup args)
    {
        CheckSupportChange(uid, false);
    }

    private void OnSupportShutdown(EntityUid uid, ZLevelSupportComponent component, ComponentShutdown args)
    {
        CheckSupportChange(uid, true);
    }

    private void OnAnchorChanged(EntityUid uid, ZLevelSupportComponent component, ref AnchorStateChangedEvent args)
    {
        // If unanchored, it stops providing support.
        // If anchored, it starts providing support.
        CheckSupportChange(uid, !args.Anchored);
    }

    private void CheckSupportChange(EntityUid supportUid, bool isRemoving)
    {
        var xform = Transform(supportUid);
        var gridUid = xform.GridUid;
        var mapUid = xform.MapUid;
        
        // During shutdown, we might have already lost our grid/map parent if we are not careful.
        // But usually it's still there during Shutdown.
        if (gridUid == null || mapUid == null)
            return;

        EntityUid? inputEnt = HasComp<CEZLevelMapComponent>(gridUid.Value) ? gridUid.Value : mapUid.Value;
        
        if (!_zLevels.TryMapUp(inputEnt.Value, out var aboveMapUid))
            return;

        if (!TryComp<MapGridComponent>(aboveMapUid.Value, out var aboveGrid))
            return;

        var worldPos = _transform.GetWorldPosition(xform);
        var indices = _mapSystem.WorldToTile(aboveMapUid.Value, aboveGrid, worldPos);
        
        EnqueueCheck(aboveMapUid.Value, aboveGrid, indices, isRemoving ? supportUid : null);
        ProcessQueue();
    }

    private void EnqueueCheck(EntityUid gridUid, MapGridComponent grid, Vector2i indices, EntityUid? ignore = null)
    {
        for (var x = -MaxStabilitySearchRange; x <= MaxStabilitySearchRange; x++)
        {
            for (var y = -MaxStabilitySearchRange; y <= MaxStabilitySearchRange; y++)
            {
                var targetPos = indices + new Vector2i(x, y);
                if (_activeChecks.Add((gridUid, targetPos)))
                {
                    _queuedChecks.Enqueue((gridUid, grid, targetPos, ignore));
                }
            }
        }
    }

    private void ProcessQueue()
    {
        if (_isProcessing)
            return;

        _isProcessing = true;
        try
        {
            while (_queuedChecks.TryDequeue(out var check))
            {
                UpdateTileStability(check.Grid, check.Component, check.Indices, check.Ignore);
                _activeChecks.Remove((check.Grid, check.Indices));
            }
        }
        finally
        {
            _isProcessing = false;
        }
    }

    private void UpdateTileStability(EntityUid gridUid, MapGridComponent grid, Vector2i indices, EntityUid? ignore)
    {
        var tile = _mapSystem.GetTileRef(gridUid, grid, indices);
        if (tile.Tile.IsEmpty)
            return;

        if (IsStable(gridUid, grid, indices, ignore))
            return;

        CollapseTile(tile, grid);
    }

    private bool IsStable(EntityUid gridUid, MapGridComponent grid, Vector2i indices, EntityUid? ignore)
    {
        var xform = Transform(gridUid);
        var mapUid = xform.MapUid;
        if (mapUid == null)
            return true;

        if (!TryComp<CEZLevelMapComponent>(gridUid, out var zMap) &&
            !TryComp<CEZLevelMapComponent>(mapUid.Value, out zMap))
            return true;

        if (zMap.Depth <= 0)
            return true;

        EntityUid? inputEnt = HasComp<CEZLevelMapComponent>(gridUid) ? gridUid : mapUid;

        if (inputEnt == null || !_zLevels.TryMapDown(inputEnt.Value, out var belowMapUid))
            return true; 

        var queue = new Queue<(Vector2i pos, int dist)>();
        var visited = new HashSet<Vector2i>();
        
        queue.Enqueue((indices, 0));
        visited.Add(indices);

        while (queue.Count > 0)
        {
            var (currPos, dist) = queue.Dequeue();

            if (CheckSupportAt(belowMapUid.Value, gridUid, grid, currPos, dist, ignore))
                return true;

            if (dist < MaxStabilitySearchRange)
            {
                foreach (var offset in Neighbors)
                {
                    var nextPos = currPos + offset;
                    if (visited.Contains(nextPos))
                        continue;

                    var nextTile = _mapSystem.GetTileRef(gridUid, grid, nextPos);
                    if (!nextTile.Tile.IsEmpty)
                    {
                        visited.Add(nextPos);
                        queue.Enqueue((nextPos, dist + 1));
                    }
                }
            }
        }

        return false;
    }

    private bool CheckSupportAt(EntityUid targetUid, EntityUid sourceGridUid, MapGridComponent sourceGrid, Vector2i sourceIndices, int distance, EntityUid? ignore)
    {
        var targetXform = Transform(targetUid);
        var mapId = targetXform.MapID;
        var worldPos = _mapSystem.GridTileToWorld(sourceGridUid, sourceGrid, sourceIndices);
        
        var box = Box2.CenteredAround(worldPos.Position, new Vector2(0.1f, 0.1f));

        foreach (var ent in _lookup.GetEntitiesIntersecting(mapId, box))
        {
            if (ent == ignore || TerminatingOrDeleted(ent))
                continue;

            if (TryComp<ZLevelSupportComponent>(ent, out var support))
            {
                // Only provide support if anchored!
                var entXform = Transform(ent);
                if (!entXform.Anchored)
                    continue;

                if (entXform.MapUid == targetUid || entXform.GridUid == targetUid)
                {
                    if (support.Radius >= distance)
                        return true;
                }
            }
        }

        return false;
    }

    private void CollapseTile(TileRef tileRef, MapGridComponent grid)
    {
        var tileDef = (ContentTileDefinition) _tileDefManager[tileRef.Tile.TypeId];
        _mapSystem.SetTile(tileRef.GridUid, grid, tileRef.GridIndices, Tile.Empty);

        var worldPos = _mapSystem.GridTileToWorld(tileRef.GridUid, grid, tileRef.GridIndices);
        var itemUid = Spawn(tileDef.ItemDropPrototypeName, worldPos);
        EnsureComp<CEZPhysicsComponent>(itemUid);
    }
}

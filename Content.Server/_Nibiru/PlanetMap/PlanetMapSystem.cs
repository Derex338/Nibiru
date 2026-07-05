using Content.Shared._Nibiru.PlanetMap;
using Content.Shared.Maps;
using Content.Shared.Parallax.Biomes;
using Content.Shared.Tag;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using System.Numerics;
using Robust.Shared.Player;

namespace Content.Server._Nibiru.PlanetMap;

/// <summary>
/// Server-side system that handles planet-map scan requests and data streaming.
///
/// Key optimisations vs the original:
/// • Scan tiles are queued and processed in batches across multiple ticks (no single-tick freeze).
/// • Saved map data is streamed to the client in chunks per tick on BUI open (no giant packet).
/// • Entity validity is checked before each Update pass so stale jobs are cleaned up.
/// </summary>
public sealed class PlanetMapSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _xform      = default!;
    [Dependency] private readonly SharedPhysicsSystem   _physics    = default!;
    [Dependency] private readonly SharedBiomeSystem     _biome      = default!;
    [Dependency] private readonly TagSystem             _tag        = default!;
    [Dependency] private readonly IMapManager           _mapManager = default!;
    [Dependency] private readonly UserInterfaceSystem   _ui         = default!;
    [Dependency] private readonly SharedMapSystem       _mapSys     = default!;
    [Dependency] private readonly IPrototypeManager     _proto      = default!;

    // -----------------------------------------------------------------------
    // Job types (runtime-only, not serialised)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Represents an in-progress scan job for one player session.
    /// Tiles are queued during OnScanRequest and classified in batches in Update().
    /// </summary>
    private sealed class ScanJob
    {
        public EntityUid     MapEnt;
        public NetEntity     MapNetEnt;
        public EntityUid     GridUid;
        public MapGridComponent Grid = default!;
        public BiomeComponent?  Biome;
        public MapId         MapId;
        public Queue<Vector2i>           RemainingTiles = new();
        public Dictionary<Vector2i, uint[]> ResultChunks  = new();
        public Dictionary<Vector2i, uint[]> ResultObjects = new();
        public ICommonSession Session   = default!;
        public int            BatchSize;
    }

    /// <summary>
    /// Represents an in-progress open-streaming job.
    /// Chunk keys are queued in OnBuiOpened and sent in batches in Update().
    /// </summary>
    private sealed class OpenJob
    {
        public EntityUid  MapEnt;
        public NetEntity  MapNetEnt;
        public Queue<Vector2i> RemainingChunks = new();
        public ICommonSession  Session  = default!;
        public int             BatchSize;
    }

    // Active jobs are tracked at system level to avoid serialisation issues.
    private readonly List<ScanJob> _activeScanJobs = new();
    private readonly List<OpenJob> _activeOpenJobs = new();

    // -----------------------------------------------------------------------
    // Lifecycle
    // -----------------------------------------------------------------------

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<PlanetMapScanRequestMessage>(OnScanRequest);

        Subs.BuiEvents<PlanetMapComponent>(
            PlanetMapUiKey.Key,
            subs => subs.Event<BoundUIOpenedEvent>(OnBuiOpened)
        );
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        ProcessOpenJobs();
        ProcessScanJobs();
    }

    // -----------------------------------------------------------------------
    // BUI Open: stream saved chunks to client
    // -----------------------------------------------------------------------

    private void OnBuiOpened(EntityUid uid, PlanetMapComponent component, BoundUIOpenedEvent args)
    {
        if (!TryComp<ActorComponent>(args.Actor, out var actor))
            return;

        var session = actor.PlayerSession;
        var netEnt  = GetNetEntity(uid);

        // Tell client: clear your local data, incoming stream coming.
        RaiseNetworkEvent(new PlanetMapOpenMessage(netEnt), session);

        if (component.SavedChunks.Count == 0)
            return; // Nothing to stream

        // Cancel any prior open job for this session (e.g. re-opened quickly)
        _activeOpenJobs.RemoveAll(j => j.Session == session);

        var job = new OpenJob
        {
            MapEnt    = uid,
            MapNetEnt = netEnt,
            Session   = session,
            BatchSize = component.OpenBatchSize,
        };
        foreach (var key in component.SavedChunks.Keys)
            job.RemainingChunks.Enqueue(key);

        _activeOpenJobs.Add(job);
    }

    private void ProcessOpenJobs()
    {
        for (var i = _activeOpenJobs.Count - 1; i >= 0; i--)
        {
            var job = _activeOpenJobs[i];

            if (!TryComp<PlanetMapComponent>(job.MapEnt, out var mapComp))
            {
                _activeOpenJobs.RemoveAt(i);
                continue;
            }

            var batchChunks  = new Dictionary<Vector2i, uint[]>();
            var batchObjects = new Dictionary<Vector2i, uint[]>();
            var sent = 0;

            while (job.RemainingChunks.Count > 0 && sent < job.BatchSize)
            {
                var key = job.RemainingChunks.Dequeue();
                if (mapComp.SavedChunks.TryGetValue(key, out var chunkArr))
                    batchChunks[key] = chunkArr;
                if (mapComp.SavedObjects.TryGetValue(key, out var objArr))
                    batchObjects[key] = objArr;
                sent++;
            }

            var isLast = job.RemainingChunks.Count == 0;

            if (batchChunks.Count > 0 || batchObjects.Count > 0)
            {
                var msg = new PlanetMapChunkBatchMessage(
                    job.MapNetEnt,
                    batchChunks,
                    batchObjects,
                    new List<string>(mapComp.ObjectPrototypes),
                    isLast);
                RaiseNetworkEvent(msg, job.Session);
            }

            if (isLast)
                _activeOpenJobs.RemoveAt(i);
        }
    }

    // -----------------------------------------------------------------------
    // Scan request: queue tiles, classify in batches across ticks
    // -----------------------------------------------------------------------

    private void OnScanRequest(PlanetMapScanRequestMessage msg, EntitySessionEventArgs args)
    {
        var player = args.SenderSession.AttachedEntity;
        if (player == null)
            return;

        if (!TryGetEntity(msg.MapEntity, out var mapEnt) ||
            !TryComp<PlanetMapComponent>(mapEnt, out var mapComp))
            return;

        var xform     = Transform(player.Value);
        var mapId     = xform.MapID;
        var playerPos = _xform.GetWorldPosition(player.Value);

        if (mapId == MapId.Nullspace)
            return;

        if (mapComp.InitialMapId != null && mapComp.InitialMapId != mapId)
            return;

        if (mapComp.InitialMapId == null)
        {
            mapComp.InitialMapId = mapId;
            Dirty(mapEnt.Value, mapComp);
        }

        if (!_mapManager.TryFindGridAt(mapId, playerPos, out var gridUid, out var grid))
            return;

        var playerTile = _mapSys.LocalToTile(gridUid, grid, xform.Coordinates);
        TryComp<BiomeComponent>(gridUid, out var biome);

        var scanRadius = mapComp.ScanRadius;

        // Cancel any prior scan job for this session (player hit scan twice quickly)
        _activeScanJobs.RemoveAll(j => j.Session == args.SenderSession);

        var job = new ScanJob
        {
            MapEnt    = mapEnt.Value,
            MapNetEnt = msg.MapEntity,
            GridUid   = gridUid,
            Grid      = grid,
            Biome     = biome,
            MapId     = mapId,
            Session   = args.SenderSession,
            BatchSize = mapComp.StreamingBatchSize,
        };

        // Pre-filter tiles: LOS check uses only integer Bresenham — fast enough to run sync.
        // The expensive ClassifyTile (GetAnchoredEntities + biome sampling) is deferred to Update().
        for (var dx = -scanRadius; dx <= scanRadius; dx++)
        {
            for (var dy = -scanRadius; dy <= scanRadius; dy++)
            {
                if (dx * dx + dy * dy > scanRadius * scanRadius) continue;
                var tile = playerTile + new Vector2i(dx, dy);
                if (mapComp.RequireVisibility && !HasLineOfSight(gridUid, playerPos, mapId, tile, grid))
                    continue;
                job.RemainingTiles.Enqueue(tile);
            }
        }

        _activeScanJobs.Add(job);
    }

    private void ProcessScanJobs()
    {
        for (var i = _activeScanJobs.Count - 1; i >= 0; i--)
        {
            var job = _activeScanJobs[i];

            // Safety: entity might have been deleted
            if (!TryComp<PlanetMapComponent>(job.MapEnt, out var mapComp) || !Exists(job.GridUid))
            {
                _activeScanJobs.RemoveAt(i);
                continue;
            }

            var processed = 0;
            while (job.RemainingTiles.Count > 0 && processed < job.BatchSize)
            {
                var tile        = job.RemainingTiles.Dequeue();
                var chunkOrigin = SharedPlanetMapSystem.GetChunkOrigin(tile);
                var relative    = SharedPlanetMapSystem.GetRelativeTile(tile, chunkOrigin);
                var index       = SharedPlanetMapSystem.GetTileIndex(relative);

                var (packedTile, packedObj) = ClassifyTile(mapComp, job.GridUid, job.Grid, tile, job.Biome, job.MapId);

                if (!job.ResultChunks.TryGetValue(chunkOrigin, out var chunkData))
                {
                    chunkData = new uint[SharedPlanetMapSystem.ArraySize];
                    job.ResultChunks[chunkOrigin] = chunkData;
                }
                if (!job.ResultObjects.TryGetValue(chunkOrigin, out var objData))
                {
                    objData = new uint[SharedPlanetMapSystem.ArraySize];
                    job.ResultObjects[chunkOrigin] = objData;
                }

                chunkData[index] = packedTile;
                objData[index]   = packedObj;
                processed++;
            }

            var isDone = job.RemainingTiles.Count == 0;

            if (isDone)
            {
                // Merge into persistent storage once the whole scan is complete
                MergeChunks(mapComp.SavedChunks,  job.ResultChunks);
                MergeChunks(mapComp.SavedObjects, job.ResultObjects);
                Dirty(job.MapEnt, mapComp);

                // Send the completed scan result to the client in one batch
                // (scan area is typically small: r=24 → ~1800 tiles → manageable packet)
                var response = new PlanetMapChunkBatchMessage(
                    job.MapNetEnt,
                    job.ResultChunks,
                    job.ResultObjects,
                    new List<string>(mapComp.ObjectPrototypes),
                    isLast: true);
                RaiseNetworkEvent(response, job.Session);

                _activeScanJobs.RemoveAt(i);
            }
        }
    }

    // -----------------------------------------------------------------------
    // Merge helper
    // -----------------------------------------------------------------------

    private static void MergeChunks(Dictionary<Vector2i, uint[]> savedMap, Dictionary<Vector2i, uint[]> newMap)
    {
        foreach (var (origin, data) in newMap)
        {
            if (!savedMap.TryGetValue(origin, out var saved))
            {
                saved = new uint[SharedPlanetMapSystem.ArraySize];
                savedMap[origin] = saved;
            }
            for (var i = 0; i < SharedPlanetMapSystem.ArraySize; i++)
            {
                if (data[i] != 0) saved[i] = data[i];
            }
        }
    }

    // -----------------------------------------------------------------------
    // Tile classification
    // -----------------------------------------------------------------------

    /// <summary>
    /// Determines the visual content of a tile (floor + object).
    /// Priority: anchored entities with hard physics or PlanetMapEntity tag > actual tile > biome virtual tile.
    /// </summary>
    private (uint tile, uint obj) ClassifyTile(
        PlanetMapComponent mapComp,
        EntityUid          gridUid,
        MapGridComponent   grid,
        Vector2i           tile,
        BiomeComponent?    biome,
        MapId              mapId)
    {
        uint objData  = 0;
        uint tileData = 0;

        // 1. Check anchored entities
        var anchored = _mapSys.GetAnchoredEntitiesEnumerator(gridUid, grid, tile);
        while (anchored.MoveNext(out var ent))
        {
            var meta = MetaData(ent.Value);
            if (meta.EntityPrototype == null) continue;

            var hasHardPhysics  = TryComp<PhysicsComponent>(ent.Value, out var physics) && physics.Hard;
            var hasPlanetMapTag = _tag.HasTag(ent.Value, "PlanetMapEntity");

            if (hasHardPhysics || hasPlanetMapTag)
            {
                var id       = meta.EntityPrototype.ID;
                var objIndex = mapComp.ObjectPrototypes.IndexOf(id);
                if (objIndex < 0)
                {
                    objIndex = mapComp.ObjectPrototypes.Count;
                    mapComp.ObjectPrototypes.Add(id);
                }
                objData = (uint)(objIndex + 1); // 0 = empty
                break;
            }
        }

        // 2. Actual tile
        if (_mapSys.TryGetTileRef(gridUid, grid, tile, out var tileRef) && !tileRef.Tile.IsEmpty)
        {
            tileData = (uint)tileRef.Tile.TypeId;
        }
        // 3. Biome virtual tile fallback
        else if (biome != null &&
                 _biome.TryGetBiomeTile(gridUid, grid, tile, out var biomeTile) &&
                 biomeTile != null)
        {
            tileData = (uint)biomeTile.Value.TypeId;
        }

        return (tileData, objData);
    }

    // -----------------------------------------------------------------------
    // Line-of-sight (Bresenham integer walk)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Returns false if any hard-physics entity blocks the straight line between player and target tile.
    /// Uses integer-only Bresenham — safe to call synchronously for every tile in the scan area.
    /// </summary>
    private bool HasLineOfSight(
        EntityUid        gridUid,
        Vector2          playerWorldPos,
        MapId            mapId,
        Vector2i         targetTile,
        MapGridComponent grid)
    {
        var playerTile = _mapSys.LocalToTile(gridUid, grid,
            new EntityCoordinates(gridUid, playerWorldPos));

        int x0 = playerTile.X, y0 = playerTile.Y;
        int x1 = targetTile.X,  y1 = targetTile.Y;

        int dx = Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
        int dy = -Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
        int err = dx + dy;

        while (true)
        {
            if (x0 == x1 && y0 == y1) break;

            var cur = new Vector2i(x0, y0);
            if (cur != playerTile && IsTileBlocking(gridUid, grid, cur))
                return false;

            var e2 = 2 * err;
            if (e2 >= dy) { err += dy; x0 += sx; }
            if (e2 <= dx) { err += dx; y0 += sy; }
        }

        return true;
    }

    private bool IsTileBlocking(EntityUid gridUid, MapGridComponent grid, Vector2i tile)
    {
        var anchored = _mapSys.GetAnchoredEntitiesEnumerator(gridUid, grid, tile);
        while (anchored.MoveNext(out var ent))
        {
            if (TryComp<PhysicsComponent>(ent.Value, out var physics) && physics.Hard)
                return true;
        }
        return false;
    }
}

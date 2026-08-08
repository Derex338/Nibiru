using Content.Shared._Nibiru.PlanetMap;
using Content.Shared.Maps;
using Content.Shared.Parallax.Biomes;
using Content.Shared.Tag;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
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
public sealed partial class PlanetMapSystem : EntitySystem
{
    private static readonly ProtoId<TagPrototype> PlanetMapEntityTag = "PlanetMapEntity";

    [Dependency] private SharedTransformSystem _xform      = default!;
    [Dependency] private SharedPhysicsSystem   _physics    = default!;
    [Dependency] private SharedBiomeSystem     _biome      = default!;
    [Dependency] private TagSystem             _tag        = default!;
    [Dependency] private IMapManager           _mapManager = default!;
    [Dependency] private UserInterfaceSystem   _ui         = default!;
    [Dependency] private SharedMapSystem       _mapSys     = default!;
    [Dependency] private IPrototypeManager     _proto      = default!;

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
        public Dictionary<Vector2i, uint[]> ResultZones   = new();
        // absolute tile -> 1-based zone prototype index (into PlanetMapComponent.ZonePrototypes)
        public Dictionary<Vector2i, int>   ZoneMembers   = new();
        // Tiles actually classified this scan. Used so the merge overwrites (incl. zeroes) ONLY
        // these tiles — others in the same chunk (outside the scan circle / behind LOS) are kept.
        public HashSet<Vector2i>           ProcessedTiles = new();
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
            var batchZones   = new Dictionary<Vector2i, uint[]>();
            var sent = 0;

            while (job.RemainingChunks.Count > 0 && sent < job.BatchSize)
            {
                var key = job.RemainingChunks.Dequeue();
                if (mapComp.SavedChunks.TryGetValue(key, out var chunkArr))
                    batchChunks[key] = chunkArr;
                if (mapComp.SavedObjects.TryGetValue(key, out var objArr))
                    batchObjects[key] = objArr;
                if (mapComp.SavedZones.TryGetValue(key, out var zoneArr))
                    batchZones[key] = zoneArr;
                sent++;
            }

            var isLast = job.RemainingChunks.Count == 0;

            if (batchChunks.Count > 0 || batchObjects.Count > 0 || batchZones.Count > 0)
            {
                var msg = new PlanetMapChunkBatchMessage(
                    job.MapNetEnt,
                    batchChunks,
                    batchObjects,
                    new List<string>(mapComp.ObjectPrototypes),
                    batchZones,
                    new List<string>(mapComp.ZonePrototypes),
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

                var (packedTile, packedObj, zoneIdx) = ClassifyTile(mapComp, job.GridUid, job.Grid, tile, job.Biome, job.MapId);

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
                job.ProcessedTiles.Add(tile);

                if (zoneIdx > 0)
                    job.ZoneMembers[tile] = zoneIdx;
                processed++;
            }

            var isDone = job.RemainingTiles.Count == 0;

            if (isDone)
            {
                // Merge into persistent storage once the whole scan is complete.
                // Only the tiles actually re-classified this scan are overwritten (incl. zeroing);
                // tiles in the same chunk that weren't scanned are preserved.
                MergeChunks(mapComp.SavedChunks,  job.ResultChunks,  job.ProcessedTiles);
                MergeChunks(mapComp.SavedObjects, job.ResultObjects, job.ProcessedTiles);

                var zoneChunks = ClassifyZones(mapComp, job.ZoneMembers);
                MergeChunks(mapComp.SavedZones, zoneChunks, job.ProcessedTiles);
                Dirty(job.MapEnt, mapComp);

                // Send the completed scan result to the client in one batch
                // (scan area is typically small: r=24 → ~1800 tiles → manageable packet)
                var response = new PlanetMapChunkBatchMessage(
                    job.MapNetEnt,
                    job.ResultChunks,
                    job.ResultObjects,
                    new List<string>(mapComp.ObjectPrototypes),
                    zoneChunks,
                    new List<string>(mapComp.ZonePrototypes),
                    isLast: true,
                    overwriteTiles: job.ProcessedTiles);
                RaiseNetworkEvent(response, job.Session);

                _activeScanJobs.RemoveAt(i);
            }
        }
    }

    // -----------------------------------------------------------------------
    // Merge helper
    // -----------------------------------------------------------------------

    /// <summary>
    /// Merges a re-scanned region into persistent storage. Only tiles listed in
    /// <paramref name="processed"/> are OVERWRITTEN (including to zero), so removed
    /// entities/objects disappear from the map on a fresh scan. Tiles in the same chunk that were
    /// not re-classified (outside the scan circle, or hidden behind LOS) are preserved.
    /// </summary>
    private static void MergeChunks(
        Dictionary<Vector2i, uint[]> savedMap,
        Dictionary<Vector2i, uint[]> newMap,
        HashSet<Vector2i> processed)
    {
        foreach (var (origin, data) in newMap)
        {
            if (!savedMap.TryGetValue(origin, out var saved))
            {
                saved = new uint[SharedPlanetMapSystem.ArraySize];
                savedMap[origin] = saved;
            }

            var baseX = origin.X * SharedPlanetMapSystem.ChunkSize;
            var baseY = origin.Y * SharedPlanetMapSystem.ChunkSize;
            for (var lx = 0; lx < SharedPlanetMapSystem.ChunkSize; lx++)
            for (var ly = 0; ly < SharedPlanetMapSystem.ChunkSize; ly++)
            {
                if (!processed.Contains(new Vector2i(baseX + lx, baseY + ly)))
                    continue;
                saved[lx * SharedPlanetMapSystem.ChunkSize + ly] =
                    data[lx * SharedPlanetMapSystem.ChunkSize + ly];
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
    private (uint tile, uint obj, int zoneIdx) ClassifyTile(
        PlanetMapComponent mapComp,
        EntityUid          gridUid,
        MapGridComponent   grid,
        Vector2i           tile,
        BiomeComponent?    biome,
        MapId              mapId)
    {
        uint objData  = 0;
        uint tileData = 0;
        var  zoneIdx  = 0;

        // 1. Check anchored entities
        var anchored = _mapSys.GetAnchoredEntitiesEnumerator(gridUid, grid, tile);
        while (anchored.MoveNext(out var ent))
        {
            var meta = MetaData(ent.Value);
            if (meta.EntityPrototype == null) continue;

            var id = meta.EntityPrototype.ID;

            // ZoneId? (forests etc.) — recorded even without hard physics/tag.
            if (zoneIdx == 0)
                zoneIdx = GetZoneIndex(mapComp, id);

            var hasHardPhysics  = TryComp<PhysicsComponent>(ent.Value, out var physics) && physics.Hard;
            var hasPlanetMapTag = _tag.HasTag(ent.Value, PlanetMapEntityTag);

            // Draw as an individual icon when it's a hard wall, tagged, OR part of a zone.
            // Zone members also go into the zone layer; sparse ones (below the density
            // threshold) render as icons while dense clusters become a blob.
            if (hasHardPhysics || hasPlanetMapTag || zoneIdx != 0)
            {
                var objIndex = mapComp.ObjectPrototypes.IndexOf(id);
                if (objIndex < 0)
                {
                    objIndex = mapComp.ObjectPrototypes.Count;
                    mapComp.ObjectPrototypes.Add(id);
                }
                if (objData == 0)
                    objData = (uint)(objIndex + 1); // 0 = empty
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

        return (tileData, objData, zoneIdx);
    }

    /// <summary>
    /// Looks up the <c>planetMapZone</c> prototype index for an entity prototype ID,
    /// registering it into <see cref="PlanetMapComponent.ZonePrototypes"/> on first use.
    /// Returns 0 when the entity does not belong to any zone.
    /// </summary>
    private int GetZoneIndex(PlanetMapComponent mapComp, string entityId)
    {
        foreach (var zone in _proto.EnumeratePrototypes<PlanetMapZonePrototype>())
        {
            foreach (var e in zone.Entities)
            {
                if (string.Equals(e, entityId, StringComparison.OrdinalIgnoreCase))
                    return GetOrAddZoneIndex(mapComp, zone.ID);
            }
            if (zone.IdPattern != null &&
                entityId.Contains(zone.IdPattern, StringComparison.OrdinalIgnoreCase))
                return GetOrAddZoneIndex(mapComp, zone.ID);
        }
        return 0;
    }

    private static int GetOrAddZoneIndex(PlanetMapComponent mapComp, string zoneId)
    {
        var idx = mapComp.ZonePrototypes.IndexOf(zoneId);
        if (idx < 0)
        {
            idx = mapComp.ZonePrototypes.Count;
            mapComp.ZonePrototypes.Add(zoneId);
        }
        return idx + 1; // 1-based; 0 = empty
    }

    /// <summary>
    /// Converts the per-tile zone members into the persistent zone chunk layer.
    /// For each zone member an entity is kept only when it has enough other members of the same
    /// zone nearby (dense cluster), i.e. it is part of a forest rather than a lone tree.
    /// </summary>
    private Dictionary<Vector2i, uint[]> ClassifyZones(
        PlanetMapComponent mapComp,
        Dictionary<Vector2i, int> members)
    {
        var result = new Dictionary<Vector2i, uint[]>();
        if (members.Count == 0)
            return result;

        // Radius lookup by 1-based zone index
        var radii = new float[mapComp.ZonePrototypes.Count + 1];
        var mins  = new int[mapComp.ZonePrototypes.Count + 1];
        // Cache of zone-ID → radius/minNeighbors, resolved once
        var zoneLookup = new Dictionary<string, (float Radius, int Min)>();
        foreach (var zone in _proto.EnumeratePrototypes<PlanetMapZonePrototype>())
            zoneLookup[zone.ID] = (zone.Radius, zone.MinNeighbors);

        for (var i = 0; i < mapComp.ZonePrototypes.Count; i++)
        {
            var protoId = mapComp.ZonePrototypes[i];
            if (zoneLookup.TryGetValue(protoId, out var cfg))
            {
                radii[i + 1] = cfg.Radius;
                mins[i + 1]  = cfg.Min;
            }
            else
            {
                radii[i + 1] = 3f;  // sensible defaults
                mins[i + 1]  = 5;
            }
        }

        // ---- Flood-fill cluster growth ----
        // A cluster begins at any member and grows outward: any member within <radius> of an
        // already-accepted member is added, which in turn widens the search radius. This growth
        // behaviour matches "two trees nearby both expand the zone, and the zone keeps growing as
        // new trees join", rather than a per-tree fixed neighbour count.
        var cellSize = SharedPlanetMapSystem.ZoneCellSize;

        // Spatial grid: cell → list of members, for fast radius queries.
        var buckets = new Dictionary<Vector2i, List<Vector2i>>();
        foreach (var (tile, idx) in members)
        {
            var cell = new Vector2i(
                (int) MathF.Floor((float) tile.X / cellSize),
                (int) MathF.Floor((float) tile.Y / cellSize));
            if (!buckets.TryGetValue(cell, out var list))
            {
                list = new List<Vector2i>();
                buckets[cell] = list;
            }
            list.Add(tile);
        }

        // ---- Flood-fill cluster growth ----
        // A cluster begins at any single member and grows outward: every member within <radius>
        // of an already-accepted member joins, which in turn widens the search radius. This
        // matches the user's intent ("two nearby trees both search nearby, find more, and the
        // zone keeps growing"), instead of a fixed per-tree neighbour count.
        // Only members inside the SAME zone id can join a cluster, so different zones never merge.
        // A cluster is only rendered as a zone once its total population reaches the prototype's
        // <minNeighbors>; smaller groups keep their individual icons.
        var done = new HashSet<Vector2i>();      // tiles already processed (any cluster seed)
        var keep = new Dictionary<Vector2i, int>(); // accepted tile -> zone id

        foreach (var (seedTile, seedZone) in members)
        {
            if (done.Contains(seedTile))
                continue;

            var rSq = radii[seedZone] * radii[seedZone];
            var cluster = new List<Vector2i>(); // members of this cluster
            var frontier = new Queue<Vector2i>();
            frontier.Enqueue(seedTile);
            done.Add(seedTile);
            cluster.Add(seedTile);

            while (frontier.Count > 0)
            {
                var a = frontier.Dequeue();
                var aCell = new Vector2i(
                    (int) MathF.Floor((float) a.X / cellSize),
                    (int) MathF.Floor((float) a.Y / cellSize));

                for (var dx = -1; dx <= 1; dx++)
                for (var dy = -1; dy <= 1; dy++)
                {
                    var nCell = new Vector2i(aCell.X + dx, aCell.Y + dy);
                    if (!buckets.TryGetValue(nCell, out var cellMembers))
                        continue;

                    foreach (var b in cellMembers)
                    {
                        if (done.Contains(b))
                            continue;
                        if (members[b] != seedZone) // only same-zone growth
                            continue;

                        var ddx = a.X - b.X;
                        var ddy = a.Y - b.Y;
                        if (ddx * ddx + ddy * ddy <= rSq)
                        {
                            done.Add(b);
                            cluster.Add(b);
                            frontier.Enqueue(b);
                        }
                    }
                }
            }

            // Accept the cluster as a zone only if it is dense enough.
            if (cluster.Count >= mins[seedZone])
            {
                foreach (var t in cluster)
                    keep[t] = seedZone;
            }
        }

        // Write accepted members into the chunk arrays
        foreach (var (tile, zoneIdx) in keep)
        {
            var origin  = SharedPlanetMapSystem.GetChunkOrigin(tile);
            var rel     = SharedPlanetMapSystem.GetRelativeTile(tile, origin);
            var index   = SharedPlanetMapSystem.GetTileIndex(rel);
            if (!result.TryGetValue(origin, out var arr))
            {
                arr = new uint[SharedPlanetMapSystem.ArraySize];
                result[origin] = arr;
            }
            arr[index] = (uint) zoneIdx;
        }

        return result;
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

    /// <summary>
    /// True if anything anchored on the tile blocks the view (does not let light / sight pass).
    /// Only an ENABLED <see cref="OccluderComponent"/> blocks — this is the signal the game's
    /// lighting/vision actually uses. Entities that are "hard" or have an Opaque collision fixture
    /// but no Occluder (trees, bushes, small plants such as flowers, grates, low fences) let
    /// light through and therefore do NOT block the map scan, matching what the player can see.
    /// </summary>
    private bool IsTileBlocking(EntityUid gridUid, MapGridComponent grid, Vector2i tile)
    {
        var anchored = _mapSys.GetAnchoredEntitiesEnumerator(gridUid, grid, tile);
        while (anchored.MoveNext(out var ent))
        {
            if (TryComp<OccluderComponent>(ent.Value, out var occluder) && occluder.Enabled)
                return true;
        }
        return false;
    }
}

using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Decals;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Shuttles.Events;
using Content.Server.Shuttles.Systems;
using Content.Shared.Atmos;
using Content.Shared.Ghost;
using Content.Shared.Gravity;
using Content.Shared.Item;
using Content.Shared.Light.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Robust.Shared.Containers;
using Content.Shared.Parallax.Biomes;
using Content.Shared.Parallax.Biomes.Layers;
using Content.Shared.Parallax.Biomes.Markers;
using Content.Shared.Tag;
using Content.Shared.Maps;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.Mapping;
using Microsoft.Extensions.ObjectPool;
using Robust.Server.Player;
using Robust.Shared;
using Robust.Shared.Collections;
using Robust.Shared.Configuration;
using Robust.Shared.Console;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Threading;
using Robust.Shared.Utility;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.EntitySerialization.Systems;
using ChunkIndicesEnumerator = Robust.Shared.Map.Enumerators.ChunkIndicesEnumerator;

namespace Content.Server.Parallax;

public sealed partial class BiomeSystem : SharedBiomeSystem
{
    [Dependency] private readonly IConfigurationManager _configManager = default!;
    [Dependency] private readonly IConsoleHost _console = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly IParallelManager _parallel = default!;
    [Dependency] private readonly ISerializationManager _serManager = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly AtmosphereSystem _atmos = default!;
    [Dependency] private readonly DecalSystem _decals = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly ShuttleSystem _shuttles = default!;
    [Dependency] private readonly TagSystem _tags = default!;
    [Dependency] private readonly TileSystem _tile = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;

    [Dependency] private EntityQuery<BiomeComponent> _biomeQuery = default!;
    [Dependency] private EntityQuery<FixturesComponent> _fixturesQuery = default!;
    [Dependency] private EntityQuery<GhostComponent> _ghostQuery = default!;
    [Dependency] private EntityQuery<TransformComponent> _xformQuery = default!;

    private readonly HashSet<EntityUid> _handledEntities = new();
    private const float DefaultLoadRange = 16f;
    private float _loadRange = DefaultLoadRange;
    private static readonly ProtoId<TagPrototype> AllowBiomeLoadingTag = "AllowBiomeLoading";
    private static readonly ProtoId<TagPrototype> BiomeMobTag = "BiomeMob";

    public bool SaveBiomeMobs { get; set; } = false;

    private List<(Vector2i, Tile)> _tiles = new();

    private ObjectPool<HashSet<Vector2i>> _tilePool =
        new DefaultObjectPool<HashSet<Vector2i>>(new SetPolicy<Vector2i>(), 256);

    /// <summary>
    /// Load area for chunks containing tiles, decals etc.
    /// </summary>
    private Box2 _loadArea = new(-DefaultLoadRange, -DefaultLoadRange, DefaultLoadRange, DefaultLoadRange);

    /// <summary>
    /// Stores the chunks active for this tick temporarily.
    /// </summary>
    private readonly Dictionary<BiomeComponent, HashSet<Vector2i>> _activeChunks = new();

    private readonly Dictionary<BiomeComponent,
        Dictionary<string, HashSet<Vector2i>>> _markerChunks = new();

    public override void Initialize()
    {
        base.Initialize();
        Log.Level = LogLevel.Debug;
        SubscribeLocalEvent<BiomeComponent, MapInitEvent>(OnBiomeMapInit);
        SubscribeLocalEvent<FTLStartedEvent>(OnFTLStarted);
        SubscribeLocalEvent<ShuttleFlattenEvent>(OnShuttleFlatten);
        Subs.CVar(_configManager, CVars.NetMaxUpdateRange, SetLoadRange, true);
        InitializeCommands();
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(ProtoReload);
        _mapLoader.OnIsSerializable += OnIsSerializable;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _mapLoader.OnIsSerializable -= OnIsSerializable;
    }

    private void OnIsSerializable(Entity<MetaDataComponent> ent, ref bool serializable)
    {
        if (serializable && !SaveBiomeMobs && _tags.HasTag(ent, BiomeMobTag))
            serializable = false;
    }

    private void ProtoReload(PrototypesReloadedEventArgs obj)
    {
        if (!obj.ByType.TryGetValue(typeof(BiomeTemplatePrototype), out var reloads))
            return;

        var query = AllEntityQuery<BiomeComponent>();

        while (query.MoveNext(out var uid, out var biome))
        {
            if (biome.Template == null || !reloads.Modified.TryGetValue(biome.Template, out var proto))
                continue;

            SetTemplate(uid, biome, (BiomeTemplatePrototype)proto);
        }
    }

    private void SetLoadRange(float obj)
    {
        // Round it up
        _loadRange = MathF.Ceiling(obj / ChunkSize) * ChunkSize;
        _loadArea = new Box2(-_loadRange, -_loadRange, _loadRange, _loadRange);
    }

    private void OnBiomeMapInit(EntityUid uid, BiomeComponent component, MapInitEvent args)
    {
        if (component.Seed == -1)
        {
            SetSeed(uid, component, _random.Next());
        }

        if (_proto.Resolve(component.Template, out var biome))
            SetTemplate(uid, component, biome);

        var xform = Transform(uid);
        var mapId = xform.MapID;

        if (mapId != MapId.Nullspace && HasComp<MapGridComponent>(uid))
        {
            var setTiles = new List<(Vector2i Index, Tile tile)>();

            foreach (var grid in _mapManager.GetAllGrids(mapId))
            {
                if (!_fixturesQuery.TryGetComponent(grid.Owner, out var fixtures))
                    continue;

                // Don't want shuttles flying around now do we.
                _shuttles.Disable(grid.Owner);
                var pTransform = _physics.GetPhysicsTransform(grid.Owner);

                foreach (var fixture in fixtures.Fixtures.Values)
                {
                    for (var i = 0; i < fixture.Shape.ChildCount; i++)
                    {
                        var aabb = fixture.Shape.ComputeAABB(pTransform, i);

                        setTiles.Clear();
                        ReserveTiles(uid, aabb, setTiles);
                    }
                }
            }
        }
    }

    public void SetEnabled(Entity<BiomeComponent?> ent, bool enabled = true)
    {
        if (!Resolve(ent, ref ent.Comp) || ent.Comp.Enabled == enabled)
            return;

        ent.Comp.Enabled = enabled;
        Dirty(ent, ent.Comp);
    }

    public void SetSeed(EntityUid uid, BiomeComponent component, int seed, bool dirty = true)
    {
        component.Seed = seed;

        if (dirty)
            Dirty(uid, component);
    }

    public void ClearTemplate(EntityUid uid, BiomeComponent component, bool dirty = true)
    {
        component.Layers.Clear();
        component.Template = null;

        if (dirty)
            Dirty(uid, component);
    }

    /// <summary>
    /// Sets the <see cref="BiomeComponent.Template"/> and refreshes layers.
    /// </summary>
    public void SetTemplate(EntityUid uid, BiomeComponent component, BiomeTemplatePrototype template, bool dirty = true)
    {
        component.Layers.Clear();
        component.Template = template.ID;

        foreach (var layer in template.Layers)
        {
            component.Layers.Add(layer);
        }

        if (dirty)
            Dirty(uid, component);
    }

    /// <summary>
    /// Adds the specified layer at the specified marker if it exists.
    /// </summary>
    public void AddLayer(EntityUid uid, BiomeComponent component, string id, IBiomeLayer addedLayer, int seedOffset = 0)
    {
        for (var i = 0; i < component.Layers.Count; i++)
        {
            var layer = component.Layers[i];

            if (layer is not BiomeDummyLayer dummy || dummy.ID != id)
                continue;

            addedLayer.Noise.SetSeed(addedLayer.Noise.GetSeed() + seedOffset);
            component.Layers.Insert(i, addedLayer);
            break;
        }

        Dirty(uid, component);
    }

    public void AddMarkerLayer(EntityUid uid, BiomeComponent component, string marker)
    {
        component.MarkerLayers.Add(marker);
        Dirty(uid, component);
    }

    /// <summary>
    /// Adds the specified template at the specified marker if it exists, withour overriding every layer.
    /// </summary>
    public void AddTemplate(EntityUid uid, BiomeComponent component, string id, BiomeTemplatePrototype template, int seedOffset = 0)
    {
        for (var i = 0; i < component.Layers.Count; i++)
        {
            var layer = component.Layers[i];

            if (layer is not BiomeDummyLayer dummy || dummy.ID != id)
                continue;

            for (var j = template.Layers.Count - 1; j >= 0; j--)
            {
                var addedLayer = template.Layers[j];
                addedLayer.Noise.SetSeed(addedLayer.Noise.GetSeed() + seedOffset);
                component.Layers.Insert(i, addedLayer);
            }

            break;
        }

        Dirty(uid, component);
    }

    private void OnFTLStarted(ref FTLStartedEvent ev)
    {
        var targetMap = _transform.ToMapCoordinates(ev.TargetCoordinates);
        var targetMapUid = _mapSystem.GetMapOrInvalid(targetMap.MapId);

        if (!TryComp<BiomeComponent>(targetMapUid, out var biome))
            return;

        var preloadArea = new Vector2(32f, 32f);
        var targetArea = new Box2(targetMap.Position - preloadArea, targetMap.Position + preloadArea);
        Preload(targetMapUid, biome, targetArea);
    }

    private void OnShuttleFlatten(ref ShuttleFlattenEvent ev)
    {
        if (!TryComp<BiomeComponent>(ev.MapUid, out var biome) ||
            !TryComp<MapGridComponent>(ev.MapUid, out var grid))
        {
            return;
        }

        var tiles = new List<(Vector2i Index, Tile Tile)>();

        foreach (var aabb in ev.AABBs)
        {
            for (var x = Math.Floor(aabb.Left); x <= Math.Ceiling(aabb.Right); x++)
            {
                for (var y = Math.Floor(aabb.Bottom); y <= Math.Ceiling(aabb.Top); y++)
                {
                    var index = new Vector2i((int)x, (int)y);
                    var chunk = SharedMapSystem.GetChunkIndices(index, ChunkSize);

                    var mod = biome.ModifiedTiles.GetOrNew(chunk * ChunkSize);

                    if (!mod.Add(index) || !TryGetVariantizedBiomeTile(index, biome.Layers, biome.Seed, (ev.MapUid, grid), out var tile))
                        continue;

                    // If we flag it as modified then the tile is never set so need to do it ourselves.
                    tiles.Add((index, tile));
                }
            }
        }

        _mapSystem.SetTiles(ev.MapUid, grid, tiles);
    }

    /// <summary>
    /// Preloads biome for the specified area.
    /// </summary>
    public void Preload(EntityUid uid, BiomeComponent component, Box2 area)
    {
        var markers = component.MarkerLayers;
        var goobers = _markerChunks.GetOrNew(component);

        foreach (var layer in markers)
        {
            var proto = ProtoManager.Index(layer);
            var enumerator = new ChunkIndicesEnumerator(area, proto.Size);

            while (enumerator.MoveNext(out var chunk))
            {
                var chunkOrigin = chunk * proto.Size;
                var layerChunks = goobers.GetOrNew(proto.ID);
                layerChunks.Add(chunkOrigin.Value);
            }
        }
    }

    private bool CanLoad(EntityUid uid)
    {
        return !_ghostQuery.HasComp(uid) || _tags.HasTag(uid, AllowBiomeLoadingTag);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var biomes = AllEntityQuery<BiomeComponent>();

        while (biomes.MoveNext(out var biome))
        {
            if (biome.LifeStage < ComponentLifeStage.Running)
                continue;

            _activeChunks.Add(biome, _tilePool.Get());
            _markerChunks.GetOrNew(biome);
        }

        // Get chunks in range
        foreach (var pSession in Filter.GetAllPlayers(_playerManager))
        {
            if (_xformQuery.TryGetComponent(pSession.AttachedEntity, out var xform) &&
                _handledEntities.Add(pSession.AttachedEntity.Value) &&
                 _biomeQuery.TryGetComponent(xform.MapUid, out var biome) &&
                biome.Enabled &&
                CanLoad(pSession.AttachedEntity.Value))
            {
                var worldPos = _transform.GetWorldPosition(xform);
                AddChunksInRange(biome, worldPos);

                foreach (var layer in biome.MarkerLayers)
                {
                    var layerProto = ProtoManager.Index(layer);
                    AddMarkerChunksInRange(biome, worldPos, layerProto);
                }
            }

            foreach (var viewer in pSession.ViewSubscriptions)
            {
                if (!_handledEntities.Add(viewer) ||
                    !_xformQuery.TryGetComponent(viewer, out xform) ||
                    !_biomeQuery.TryGetComponent(xform.MapUid, out biome) ||
                    !biome.Enabled ||
                    !CanLoad(viewer))
                {
                    continue;
                }

                var worldPos = _transform.GetWorldPosition(xform);
                AddChunksInRange(biome, worldPos);

                foreach (var layer in biome.MarkerLayers)
                {
                    var layerProto = ProtoManager.Index(layer);
                    AddMarkerChunksInRange(biome, worldPos, layerProto);
                }
            }
        }

        var loadBiomes = AllEntityQuery<BiomeComponent, MapGridComponent>();
        var sw = new System.Diagnostics.Stopwatch();
        sw.Start();

        while (loadBiomes.MoveNext(out var gridUid, out var biome, out var grid))
        {
            // If not MapInit don't run it.
            if (biome.LifeStage < ComponentLifeStage.Running)
                continue;

            if (!biome.Enabled)
                continue;

            // Load new chunks
            LoadChunks(biome, gridUid, grid, biome.Seed, sw);
            // Unload old chunks
            UnloadChunks(biome, gridUid, grid, biome.Seed, sw);
        }

        _handledEntities.Clear();

        foreach (var tiles in _activeChunks.Values)
        {
            _tilePool.Return(tiles);
        }

        _activeChunks.Clear();
        _markerChunks.Clear();
    }

    private void AddChunksInRange(BiomeComponent biome, Vector2 worldPos)
    {
        var enumerator = new ChunkIndicesEnumerator(_loadArea.Translated(worldPos), ChunkSize);

        while (enumerator.MoveNext(out var chunkOrigin))
        {
            _activeChunks[biome].Add(chunkOrigin.Value * ChunkSize);
        }
    }

    private void AddMarkerChunksInRange(BiomeComponent biome, Vector2 worldPos, IBiomeMarkerLayer layer)
    {
        // Offset the load area so it's centralised.
        var loadArea = new Box2(0, 0, layer.Size, layer.Size);
        var halfLayer = new Vector2(layer.Size / 2f);

        var enumerator = new ChunkIndicesEnumerator(loadArea.Translated(worldPos - halfLayer), layer.Size);

        while (enumerator.MoveNext(out var chunkOrigin))
        {
            var lay = _markerChunks[biome].GetOrNew(layer.ID);
            lay.Add(chunkOrigin.Value * layer.Size);
        }
    }

    #region Load

    /// <summary>
    /// Loads all of the chunks for a particular biome, as well as handle any marker chunks.
    /// </summary>
    private void LoadChunks(
        BiomeComponent component,
        EntityUid gridUid,
        MapGridComponent grid,
        int seed,
        System.Diagnostics.Stopwatch sw)
    {
        BuildMarkerChunks(component, gridUid, grid, seed);

        var active = _activeChunks[component];
        var chunksLoaded = 0;

        foreach (var chunk in active)
        {
            LoadChunkMarkers(component, gridUid, grid, chunk, seed);

            if (component.LoadedChunks.Contains(chunk))
                continue;

            // Prevent generation lag spike by capping chunk loads per tick
            if (sw.Elapsed.TotalMilliseconds > 8 && chunksLoaded > 0)
                continue;

            if (!component.LoadedChunks.Add(chunk))
                continue;

            // Load NOW!
            LoadChunk(component, gridUid, grid, chunk, seed);
            chunksLoaded++;
        }
    }

    /// <summary>
    /// Goes through all marker chunks that haven't been calculated, then calculates what spawns there are and
    /// allocates them to the relevant actual chunks in the biome (marker chunks may be many times larger than biome chunks).
    /// </summary>
    private void BuildMarkerChunks(BiomeComponent component, EntityUid gridUid, MapGridComponent grid, int seed)
    {
        var markers = _markerChunks[component];
        var loadedMarkers = component.LoadedMarkers;
        var idx = 0;

        foreach (var (layer, chunks) in markers)
        {
            // I know dictionary ordering isn't guaranteed but I just need something to differentiate seeds.
            idx++;
            var localIdx = idx;

            Parallel.ForEach(chunks, new ParallelOptions() { MaxDegreeOfParallelism = _parallel.ParallelProcessCount }, chunk =>
            {
                if (loadedMarkers.TryGetValue(layer, out var mobChunks) && mobChunks.Contains(chunk))
                    return;

                var forced = component.ForcedMarkerLayers.Contains(layer);

                // Make a temporary version and copy back in later.
                var pending = new Dictionary<Vector2i, Dictionary<string, List<Vector2i>>>();

                // Essentially get the seed + work out a buffer to adjacent chunks so we don't
                // inadvertantly spawn too many near the edges.
                var layerProto = ProtoManager.Index<BiomeMarkerLayerPrototype>(layer);
                var markerSeed = seed + chunk.X * ChunkSize + chunk.Y + localIdx;
                var rand = new RobustRandom();
                rand.SetSeed(markerSeed);
                var buffer = (int)(layerProto.Radius / 2f);
                var bounds = new Box2i(chunk + buffer, chunk + layerProto.Size - buffer);
                var count = (int)(bounds.Area / (layerProto.Radius * layerProto.Radius));
                count = Math.Min(count, layerProto.MaxCount);

                GetMarkerNodes(gridUid, component, grid, layerProto, forced, bounds, count, rand,
                    out var spawnSet, out var existing);

                // Forcing markers to spawn so delete any that were found to be in the way.
                if (forced && existing.Count > 0)
                {
                    // Lock something so we can delete these safely.
                    lock (component.PendingMarkers)
                    {
                        foreach (var ent in existing)
                        {
                            Del(ent);
                        }
                    }
                }

                foreach (var node in spawnSet.Keys)
                {
                    var chunkOrigin = SharedMapSystem.GetChunkIndices(node, ChunkSize) * ChunkSize;

                    if (!pending.TryGetValue(chunkOrigin, out var pendingMarkers))
                    {
                        pendingMarkers = new Dictionary<string, List<Vector2i>>();
                        pending[chunkOrigin] = pendingMarkers;
                    }

                    if (!pendingMarkers.TryGetValue(layer, out var layerMarkers))
                    {
                        layerMarkers = new List<Vector2i>();
                        pendingMarkers[layer] = layerMarkers;
                    }

                    layerMarkers.Add(node);
                }

                lock (loadedMarkers)
                {
                    if (!loadedMarkers.TryGetValue(layer, out var lockMobChunks))
                    {
                        lockMobChunks = new HashSet<Vector2i>();
                        loadedMarkers[layer] = lockMobChunks;
                    }

                    lockMobChunks.Add(chunk);

                    foreach (var (chunkOrigin, layers) in pending)
                    {
                        if (!component.PendingMarkers.TryGetValue(chunkOrigin, out var lockMarkers))
                        {
                            lockMarkers = new Dictionary<string, List<Vector2i>>();
                            component.PendingMarkers[chunkOrigin] = lockMarkers;
                        }

                        foreach (var (lockLayer, nodes) in layers)
                        {
                            lockMarkers[lockLayer] = nodes;
                        }
                    }
                }
            });
        }

        component.ForcedMarkerLayers.Clear();
    }

    /// <summary>
    /// Gets the marker nodes for the specified area.
    /// </summary>
    /// <param name="emptyTiles">Should we include empty tiles when determine markers (e.g. if they are yet to be loaded)</param>
    public void GetMarkerNodes(
        EntityUid gridUid,
        BiomeComponent biome,
        MapGridComponent grid,
        BiomeMarkerLayerPrototype layerProto,
        bool forced,
        Box2i bounds,
        int count,
        IRobustRandom rand,
        out Dictionary<Vector2i, string?> spawnSet,
        out HashSet<EntityUid> existingEnts,
        bool emptyTiles = true)
    {
        DebugTools.Assert(count > 0);
        var remainingTiles = _tilePool.Get();
        var nodeEntities = new Dictionary<Vector2i, EntityUid?>();
        var nodeMask = new Dictionary<Vector2i, string?>();

        // Okay so originally we picked a random tile and BFS outwards
        // the problem is if you somehow get a cooked frontier then it might drop entire veins
        // hence we'll grab all valid tiles up front and use that as possible seeds.
        // It's hella more expensive but stops issues.
        for (var x = bounds.Left; x < bounds.Right; x++)
        {
            for (var y = bounds.Bottom; y < bounds.Top; y++)
            {
                var node = new Vector2i(x, y);

                // Empty tile, skip if relevant.
                if (!emptyTiles && (!_mapSystem.TryGetTile(grid, node, out var tile) || tile.IsEmpty))
                    continue;

                // Check if it's a valid spawn, if so then use it.
                var enumerator = _mapSystem.GetAnchoredEntitiesEnumerator(gridUid, grid, node);
                enumerator.MoveNext(out var existing);

                if (!forced && existing != null)
                    continue;

                // Check if mask matches // anything blocking.
                TryGetEntity(node, biome, (gridUid, grid), out var proto);

                // If there's an existing entity and it doesn't match the mask then skip.
                if (layerProto.EntityMask.Count > 0 &&
                    (proto == null ||
                     !layerProto.EntityMask.ContainsKey(proto)))
                {
                    continue;
                }

                // If it's just a flat spawn then just check for anything blocking.
                if (proto != null && layerProto.Prototype != null)
                {
                    continue;
                }

                DebugTools.Assert(layerProto.EntityMask.Count == 0 || !string.IsNullOrEmpty(proto));
                remainingTiles.Add(node);
                nodeEntities.Add(node, existing);
                nodeMask.Add(node, proto);
            }
        }

        var frontier = new ValueList<Vector2i>(32);
        // TODO: Need poisson but crashes whenever I use moony's due to inputs or smth idk
        // Get the total amount of groups to spawn across the entire chunk.
        // We treat a null entity mask as requiring nothing else on the tile

        spawnSet = new Dictionary<Vector2i, string?>();
        existingEnts = new HashSet<EntityUid>();

        // Iterate the group counts and pathfind out each group.
        for (var i = 0; i < count; i++)
        {
            var groupSize = rand.Next(layerProto.MinGroupSize, layerProto.MaxGroupSize + 1);

            // While we have remaining tiles keep iterating
            while (groupSize > 0 && remainingTiles.Count > 0)
            {
                var startNode = rand.Pick(remainingTiles);
                remainingTiles.Remove(startNode);
                frontier.Clear();
                frontier.Add(startNode);

                // This essentially may lead to a vein being split in multiple areas but the count matters more than position.
                while (frontier.Count > 0 && groupSize > 0)
                {
                    // Need to pick a random index so we don't just get straight lines of ores.
                    var frontierIndex = rand.Next(frontier.Count);
                    var node = frontier[frontierIndex];
                    frontier.RemoveSwap(frontierIndex);
                    remainingTiles.Remove(node);

                    // Add neighbors if they're valid, worst case we add no more and pick another random seed tile.
                    for (var x = -1; x <= 1; x++)
                    {
                        for (var y = -1; y <= 1; y++)
                        {
                            var neighbor = new Vector2i(node.X + x, node.Y + y);

                            if (frontier.Contains(neighbor) || !remainingTiles.Contains(neighbor))
                                continue;

                            frontier.Add(neighbor);
                        }
                    }

                    // Tile valid salad so add it.
                    var mask = nodeMask[node];
                    spawnSet.Add(node, mask);
                    groupSize--;

                    if (nodeEntities.TryGetValue(node, out var existing))
                    {
                        Del(existing);
                    }
                }
            }

            if (groupSize > 0)
            {
                Log.Warning($"Found remaining group size for ore veins!");
            }
        }

        _tilePool.Return(remainingTiles);
    }

    /// <summary>
    /// Loads the pre-deteremined marker nodes for a particular chunk.
    /// This is calculated in <see cref="BuildMarkerChunks"/>
    /// </summary>
    /// <remarks>
    /// Note that the marker chunks do not correspond to this chunk.
    /// </remarks>
    private void LoadChunkMarkers(
        BiomeComponent component,
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i chunk,
        int seed)
    {
        // Load any pending marker tiles first.
        if (!component.PendingMarkers.TryGetValue(chunk, out var layers))
            return;

        // This needs to be done separately in case we try to add a marker layer and want to force it on existing
        // loaded chunks.
        component.ModifiedTiles.TryGetValue(chunk, out var modified);
        modified ??= _tilePool.Get();

        foreach (var (layer, nodes) in layers)
        {
            var layerProto = ProtoManager.Index<BiomeMarkerLayerPrototype>(layer);

            foreach (var node in nodes)
            {
                if (modified.Contains(node))
                    continue;

                // Need to ensure the tile under it has loaded for anchoring.
                if (TryGetVariantizedBiomeTile(node, component.Layers, seed, (gridUid, grid), out var tile))
                {
                    _mapSystem.SetTile(gridUid, grid, node, tile);
                }

                string? prototype;

                if (TryGetEntity(node, component, (gridUid, grid), out var proto) &&
                    layerProto.EntityMask.TryGetValue(proto, out var maskedProto))
                {
                    prototype = maskedProto;
                }
                else
                {
                    prototype = layerProto.Prototype;
                }

                // If it is a ghost role then purge it
                // TODO: This is *kind* of a bandaid but natural mobs spawns needs a lot more work.
                // Ideally we'd just have ghost role and non-ghost role variants for some stuff.
                var uid = EntityManager.CreateEntityUninitialized(prototype, _mapSystem.GridTileToLocal(gridUid, grid, node));
                RemComp<GhostTakeoverAvailableComponent>(uid);
                RemComp<GhostRoleComponent>(uid);
                EntityManager.InitializeAndStartEntity(uid);

                if (HasComp<MobStateComponent>(uid))
                    _tags.AddTag(uid, BiomeMobTag);

                if (component.LoadedEntities.TryGetValue(chunk, out var loadedEntities))
                {
                    loadedEntities.Add(uid, node);
                }
                else
                {
                    loadedEntities = new Dictionary<EntityUid, Vector2i>();
                    loadedEntities.Add(uid, node);
                    component.LoadedEntities.Add(chunk, loadedEntities);
                }
            }
        }

        if (modified.Count == 0)
        {
            component.ModifiedTiles.Remove(chunk);
            _tilePool.Return(modified);
        }

        component.PendingMarkers.Remove(chunk);
    }

    /// <summary>
    /// Loads a particular queued chunk for a biome.
    /// </summary>
    private void LoadChunk(
        BiomeComponent component,
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i chunk,
        int seed)
    {
        component.ModifiedTiles.TryGetValue(chunk, out var modified);
        modified ??= _tilePool.Get();
        _tiles.Clear();

        // Set tiles first
        for (var x = 0; x < ChunkSize; x++)
        {
            for (var y = 0; y < ChunkSize; y++)
            {
                var indices = new Vector2i(x + chunk.X, y + chunk.Y);

                // Pass in null so we don't try to get the tileref.
                if (modified.Contains(indices))
                    continue;

                // If there's existing data then don't overwrite it.
                if (_mapSystem.TryGetTileRef(gridUid, grid, indices, out var tileRef) && !tileRef.Tile.IsEmpty)
                    continue;

                if (!TryGetVariantizedBiomeTile(indices, component.Layers, seed, (gridUid, grid), out var biomeTile))
                    continue;

                _tiles.Add((indices, biomeTile));
            }
        }

        _mapSystem.SetTiles(gridUid, grid, _tiles);
        _tiles.Clear();

        // Now do entities
        if (!component.LoadedEntities.TryGetValue(chunk, out var loadedEntities))
        {
            loadedEntities = new Dictionary<EntityUid, Vector2i>();
            component.LoadedEntities.Add(chunk, loadedEntities);
        }

        for (var x = 0; x < ChunkSize; x++)
        {
            for (var y = 0; y < ChunkSize; y++)
            {
                var indices = new Vector2i(x + chunk.X, y + chunk.Y);

                if (modified.Contains(indices))
                    continue;

                // Don't mess with anything that's potentially anchored.
                var anchored = _mapSystem.GetAnchoredEntitiesEnumerator(gridUid, grid, indices);

                if (anchored.MoveNext(out _) || !TryGetEntity(indices, component, (gridUid, grid), out var entPrototype))
                    continue;

                // TODO: Fix non-anchored ents spawning.
                // Just track loaded chunks for now.
                var ent = Spawn(entPrototype, _mapSystem.GridTileToLocal(gridUid, grid, indices));

                // At least for now unless we do lookups or smth, only work with anchoring.
                if (_xformQuery.TryGetComponent(ent, out var xform) && !xform.Anchored)
                {
                    if (!HasComp<MobStateComponent>(ent) && !HasComp<ItemComponent>(ent) && !HasComp<PullableComponent>(ent))
                        _transform.AnchorEntity((ent, xform), (gridUid, grid), indices);
                }

                if (HasComp<MobStateComponent>(ent))
                    _tags.AddTag(ent, BiomeMobTag);

                loadedEntities.Add(ent, indices);
            }
        }

        // Decals
        var loadedDecals = new Dictionary<uint, Vector2i>();
        component.LoadedDecals.Add(chunk, loadedDecals);

        for (var x = 0; x < ChunkSize; x++)
        {
            for (var y = 0; y < ChunkSize; y++)
            {
                var indices = new Vector2i(x + chunk.X, y + chunk.Y);

                if (modified.Contains(indices))
                    continue;

                // Don't mess with anything that's potentially anchored.
                var anchored = _mapSystem.GetAnchoredEntitiesEnumerator(gridUid, grid, indices);

                if (anchored.MoveNext(out _) || !TryGetDecals(indices, component.Layers, seed, (gridUid, grid), out var decals))
                    continue;

                foreach (var decal in decals)
                {
                    if (!_decals.TryAddDecal(decal.ID, new EntityCoordinates(gridUid, decal.Position), out var dec))
                        continue;

                    loadedDecals.Add(dec, indices);
                }
            }
        }

        if (modified.Count == 0)
        {
            _tilePool.Return(modified);
            component.ModifiedTiles.Remove(chunk);
        }
        else
        {
            component.ModifiedTiles[chunk] = modified;
        }
    }

    #endregion

    #region Unload

    /// <summary>
    /// Handles all of the queued chunk unloads for a particular biome.
    /// </summary>
    private void UnloadChunks(BiomeComponent component, EntityUid gridUid, MapGridComponent grid, int seed, System.Diagnostics.Stopwatch sw)
    {
        var active = _activeChunks[component];
        List<(Vector2i, Tile)>? tiles = null;

        var toUnload = new ValueList<Vector2i>();
        foreach (var chunk in component.LoadedChunks)
        {
            if (!active.Contains(chunk))
                toUnload.Add(chunk);
        }

        var chunksUnloaded = 0;
        foreach (var chunk in toUnload)
        {
            if (sw.Elapsed.TotalMilliseconds > 20 && chunksUnloaded > 0)
                break;

            if (!component.LoadedChunks.Remove(chunk))
                continue;

            // Unload NOW!
            tiles ??= new List<(Vector2i, Tile)>(ChunkSize * ChunkSize);
            UnloadChunk(component, gridUid, grid, chunk, seed, tiles);
            chunksUnloaded++;
        }
    }

    /// <summary>
    /// Unloads a specific biome chunk.
    /// </summary>
    private void UnloadChunk(BiomeComponent component, EntityUid gridUid, MapGridComponent grid, Vector2i chunk, int seed, List<(Vector2i, Tile)> tiles)
    {
        // Reverse order to loading
        component.ModifiedTiles.TryGetValue(chunk, out var modified);
        modified ??= new HashSet<Vector2i>();

        // Delete decals
        foreach (var (dec, indices) in component.LoadedDecals[chunk])
        {
            // If we couldn't remove it then flag the tile to never be touched.
            if (!_decals.RemoveDecal(gridUid, dec))
            {
                modified.Add(indices);
            }
        }

        component.LoadedDecals.Remove(chunk);

        // Delete entities
        // Ideally any entities that aren't modified just get deleted and re-generated later
        // This is because if we want to save the map (e.g. persistent server) it makes the file much smaller
        // and also if the map is enormous will make stuff like physics broadphase much faster
        var xformQuery = GetEntityQuery<TransformComponent>();
        var metaQuery = GetEntityQuery<MetaDataComponent>();

        if (component.LoadedEntities.TryGetValue(chunk, out var loaded))
        {
            foreach (var (ent, tile) in loaded)
            {
                if (Deleted(ent) || !xformQuery.TryGetComponent(ent, out var xform) || !metaQuery.TryGetComponent(ent, out var metadata))
                {
                modified.Add(tile);
                    continue;
                }

                // It's moved to another tile
                if (_mapSystem.LocalToTile(gridUid, grid, xform.Coordinates) != tile)
                {
                modified.Add(tile);
                    continue;
                }

                // Is it picked up or in a container?
                if (xform.ParentUid != gridUid)
                {
                modified.Add(tile);
                    continue;
                }

                // Has it been renamed/modified by player?
                if (metadata.EntityPrototype != null &&
                    (metadata.EntityName != metadata.EntityPrototype.Name ||
                     metadata.EntityDescription != metadata.EntityPrototype.Description))
                {
                modified.Add(tile);
                    continue;
                }

                // If it's an item, did it move from its exact spawn position?
                if (HasComp<ItemComponent>(ent))
                {
                    var spawnCoords = _mapSystem.GridTileToLocal(gridUid, grid, tile);
                    if (!xform.Coordinates.Position.EqualsApprox(spawnCoords.Position, 0.01f))
                    {
                        modified.Add(tile);
                        continue;
                    }
                }

                // 1. Проверяем, должен ли этот ентити здесь находиться (мягкая проверка имени прототипа и маркеров)
                if (!IsExpectedByGenerator(ent, tile, metadata.EntityPrototype?.ID, component, gridUid, grid))
                {
                    modified.Add(tile);
                    continue;
                }

                Del(ent);
            }

            component.LoadedEntities.Remove(chunk);
        }

        // Unset tiles (if the data is custom)

        for (var x = 0; x < ChunkSize; x++)
        {
            for (var y = 0; y < ChunkSize; y++)
            {
                var indices = new Vector2i(x + chunk.X, y + chunk.Y);

                if (modified.Contains(indices))
                    continue;

                // Don't mess with anything that's potentially anchored.
                var anchored = _mapSystem.GetAnchoredEntitiesEnumerator(gridUid, grid, indices);

                if (anchored.MoveNext(out _))
                {
                    modified.Add(indices);
                    continue;
                }

                var entities = _turf.GetEntitiesInTile(_mapSystem.GridTileToLocal(gridUid, grid, indices), LookupFlags.Dynamic | LookupFlags.Sundries | LookupFlags.Approximate | LookupFlags.Uncontained);

                foreach (var ent in entities)
                {
                    if (Deleted(ent) || ent == gridUid)
                        continue;

                    if (!TryComp<MetaDataComponent>(ent, out var metadata))
                    {
                        modified.Add(indices);
                        break;
                    }

                    if (!IsExpectedByGenerator(ent, indices, metadata.EntityPrototype?.ID, component, gridUid, grid))
                    {
                        modified.Add(indices);
                        break;
                    }

                    // 2. Для предметов проверяем, не сдвинул ли их игрок со спавна
                    if (HasComp<ItemComponent>(ent))
                    {
                        var spawnCoords = _mapSystem.GridTileToLocal(gridUid, grid, indices);
                        if (!xformQuery.GetComponent(ent).Coordinates.Position.EqualsApprox(spawnCoords.Position, 0.01f))
                        {
                            modified.Add(indices);
                            break;
                        }
                    }
                }

                if (modified.Contains(indices))
                    continue;

                // Если мы здесь, значит все сущности на тайле - "родные" и не измененные.
                // Удаляем их перед отгрузкой тайла, чтобы не было дублей при повторной загрузке.
                foreach (var ent in entities)
                {
                    if (Deleted(ent) || ent == gridUid)
                        continue;
                    Del(ent);
                }

                // If it's default data unload the tile.
                if (!TryGetVariantizedBiomeTile(indices, component.Layers, seed, null, out var biomeTile))
                {
                    modified.Add(indices);
                    continue;
                }

                if (_mapSystem.TryGetTileRef(gridUid, grid, indices, out var tileRef) && tileRef.Tile != biomeTile)
                {
                    modified.Add(indices);
                    continue;
                }

                tiles.Add((indices, Tile.Empty));
            }
        }

        _mapSystem.SetTiles(gridUid, grid, tiles);
        tiles.Clear();
        component.LoadedChunks.Remove(chunk);

        if (modified.Count == 0)
        {
            component.ModifiedTiles.Remove(chunk);
        }
        else
        {
            component.ModifiedTiles[chunk] = modified;
        }
    }

    #endregion

    /// <summary>
    /// Creates a simple planet setup for a map.
    /// </summary>
    public void EnsurePlanet(EntityUid mapUid, BiomeTemplatePrototype biomeTemplate, int? seed = null, MetaDataComponent? metadata = null, Color? mapLight = null)
    {
        if (!Resolve(mapUid, ref metadata))
            return;

        EnsureComp<MapGridComponent>(mapUid);
        var biome = EntityManager.ComponentFactory.GetComponent<BiomeComponent>();
        seed ??= _random.Next();
        SetSeed(mapUid, biome, seed.Value, false);
        SetTemplate(mapUid, biome, biomeTemplate, false);
        AddComp(mapUid, biome, true);
        Dirty(mapUid, biome, metadata);

        var gravity = EnsureComp<GravityComponent>(mapUid);
        gravity.Enabled = true;
        gravity.Inherent = true;
        Dirty(mapUid, gravity, metadata);

        // Day lighting
        // Daylight: #D8B059
        // Midday: #E6CB8B
        // Moonlight: #2b3143
        // Lava: #A34931
        var light = EnsureComp<MapLightComponent>(mapUid);
        light.AmbientLightColor = mapLight ?? Color.FromHex("#D8B059");
        Dirty(mapUid, light, metadata);

        EnsureComp<RoofComponent>(mapUid);

        EnsureComp<LightCycleComponent>(mapUid);

        EnsureComp<SunShadowComponent>(mapUid);
        EnsureComp<SunShadowCycleComponent>(mapUid);

        var moles = new float[Atmospherics.AdjustedNumberOfGases];
        moles[(int)Gas.Oxygen] = 21.824779f;
        moles[(int)Gas.Nitrogen] = 82.10312f;

        var mixture = new GasMixture(moles, Atmospherics.T20C);

        _atmos.SetMapAtmosphere(mapUid, false, mixture);
    }

    /// <summary>
    /// Sets the specified tiles as relevant and marks them as modified.
    /// </summary>
    public void ReserveTiles(EntityUid mapUid, Box2 bounds, List<(Vector2i Index, Tile Tile)> tiles, BiomeComponent? biome = null, MapGridComponent? mapGrid = null)
    {
        if (!Resolve(mapUid, ref biome, ref mapGrid, false))
            return;

        foreach (var tileSet in _mapSystem.GetLocalTilesIntersecting(mapUid, mapGrid, bounds, false))
        {
            Vector2i chunkOrigin;
            HashSet<Vector2i> modified;

            // Existing, ignore
            if (_mapSystem.TryGetTileRef(mapUid, mapGrid, tileSet.GridIndices, out var existingRef) && !existingRef.Tile.IsEmpty)
            {
                chunkOrigin = SharedMapSystem.GetChunkIndices(tileSet.GridIndices, ChunkSize) * ChunkSize;
                modified = biome.ModifiedTiles.GetOrNew(chunkOrigin);
                modified.Add(tileSet.GridIndices);
                continue;
            }

            if (!TryGetVariantizedBiomeTile(tileSet.GridIndices, biome.Layers, biome.Seed, (mapUid, mapGrid), out var tile))
            {
                continue;
            }

            chunkOrigin = SharedMapSystem.GetChunkIndices(tileSet.GridIndices, ChunkSize) * ChunkSize;
            modified = biome.ModifiedTiles.GetOrNew(chunkOrigin);
            modified.Add(tileSet.GridIndices);
            tiles.Add((tileSet.GridIndices, tile));
        }

        _mapSystem.SetTiles(mapUid, mapGrid, tiles);
    }

    private bool TryGetVariantizedBiomeTile(Vector2i index, List<IBiomeLayer> layers, int seed, Entity<MapGridComponent>? component, out Tile tile)
    {
        if (!TryGetBiomeTile(index, layers, seed, component, out var biomeTile) || biomeTile == null)
        {
            tile = Tile.Empty;
            return false;
        }

        var def = _turf.GetContentTileDefinition(biomeTile.Value);

        // Используем хеширование на основе MurmurHash3
        int tileSeed = unchecked(seed ^ (index.X * 397) ^ (index.Y * 3931));
        tileSeed = unchecked((tileSeed ^ (tileSeed >> 16)) * 0x45d9f3b);
        tileSeed = unchecked((tileSeed ^ (tileSeed >> 16)) * 0x45d9f3b);
        tileSeed = unchecked(tileSeed ^ (tileSeed >> 16));

        tile = new Tile(biomeTile.Value.TypeId, biomeTile.Value.Flags, _tile.PickVariant(def, tileSeed), biomeTile.Value.RotationMirroring);
        return true;
    }

    private bool IsExpectedByGenerator(EntityUid ent, Vector2i tile, string? currentProto, BiomeComponent biome, EntityUid gridUid, MapGridComponent grid)
    {
        // Если у сущности нет прототипа (игроки, системные объекты), мы не считаем это модификацией биома.
        if (currentProto == null) return true;

        // Биомные мобы могут свободно перемещаться, их наличие на "неродном" тайле не является модификацией.
        if (_tags.HasTag(ent, BiomeMobTag))
            return true;

        // Если сущность получила урон или была сильно изменена игроком, она больше не должна считаться "ожидаемой"
        // Это заменяет медленную проверку IsBiomeDefault для разрушаемых объектов.
        if (TryComp<DamageableComponent>(ent, out var damage) && _damageable.GetTotalDamage((ent, damage)) > FixedPoint2.Zero)
            return false;

        // 1. Проверяем базовый слой
        if (TryGetEntity(tile, biome, (gridUid, grid), out var expectedProto) && expectedProto != null)
        {
            if (currentProto == expectedProto)
            {
                return true;
            }
        }

        // 2. Проверяем маркеры.
        foreach (var markerId in biome.MarkerLayers)
        {
            var layerProto = ProtoManager.Index<BiomeMarkerLayerPrototype>(markerId);
            if (layerProto.Prototype == currentProto || layerProto.EntityMask.Values.Any(v => v.Id == currentProto))
            {
                return true;
            }
        }

        return false;
    }

    private static readonly HashSet<string> _ignoredBiomeComps = new()
    {
        "CEActiveZPhysics", "CEZPhysics", "SolutionRegeneration", "Battery"
    };

    private bool IsBiomeDefault(EntityUid uid, ICollection<string>? ignoredComps)
    {
        if (!EntityManager.TryGetComponent<MetaDataComponent>(uid, out var metadata) || metadata.EntityPrototype == null)
            return false;

        var prototype = metadata.EntityPrototype;

        // Если игрок переименовал сущность - она модифицирована
        if (metadata.EntityName != prototype.Name || metadata.EntityDescription != prototype.Description)
            return false;

        var protoData = _proto.GetPrototypeData(prototype);

        foreach (var component in EntityManager.GetComponents(uid))
        {
            if (component.Deleted)
                continue;

            var compType = component.GetType();
            if (compType == typeof(TransformComponent) || compType == typeof(MetaDataComponent))
                continue;

            var compName = EntityManager.ComponentFactory.GetComponentName(compType);
            if (ignoredComps?.Contains(compName) == true)
                continue;

            if (!protoData.TryGetValue(compName, out var protoMapping))
                continue;

            try
            {
                var diffFromDefault = _serManager.WriteValueAs<MappingDataNode>(compType, component, alwaysWrite: false);
                if (diffFromDefault.AnyExcept(protoMapping))
                    return false;
            }
            catch
            {
                // В случае ошибки сериализации считаем модифицированным
                return false;
            }
        }

        return true;
    }
}

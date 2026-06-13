using Content.Shared._Nibiru.PlanetMap;
using Content.Shared.Maps;
using Content.Shared.Parallax.Biomes;
using Content.Shared.Parallax.Biomes.Layers;
using Content.Shared.Tag;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Content.Shared.Interaction.Events;
using System.Numerics;
using Robust.Shared.Player;
using Robust.Shared.Physics.Components;

namespace Content.Server._Nibiru.PlanetMap;

/// <summary>
/// Server-side system that handles planet-map scan requests.
/// When a player presses the "pen" button it:
/// 1. Finds all chunks loaded within the scan radius.
/// 2. For each tile, determines its visual category.
/// 3. Optionally masks off tiles not visible from the player (LOS).
/// 4. Saves data to PlanetMapComponent on the map item.
/// 5. Sends the new chunk data to the requesting client.
/// </summary>
public sealed class PlanetMapSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem  _xform      = default!;
    [Dependency] private readonly SharedPhysicsSystem    _physics    = default!;
    [Dependency] private readonly SharedBiomeSystem      _biome      = default!;
    [Dependency] private readonly TagSystem              _tag        = default!;
    [Dependency] private readonly IMapManager            _mapManager = default!;
    [Dependency] private readonly UserInterfaceSystem    _ui         = default!;
    [Dependency] private readonly SharedMapSystem        _mapSys     = default!;
    [Dependency] private readonly IPrototypeManager      _proto      = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<PlanetMapScanRequestMessage>(OnScanRequest);
        SubscribeNetworkEvent<PlanetMapOpenMessage>(OnMapOpened);
        //SubscribeLocalEvent<PlanetMapComponent, UseInHandEvent>(OnUseInHand);

        Subs.BuiEvents<PlanetMapComponent>(
            PlanetMapUiKey.Key,
            subs => subs.Event<BoundUIOpenedEvent>(OnBuiOpened)
        );
    }

    private void OnBuiOpened(EntityUid uid, PlanetMapComponent component, BoundUIOpenedEvent args)
    {
        //if (args.Handled) return;

        //if (_ui.TryOpenUi(uid, PlanetMapUiKey.Key, args.User))
        //{
            // Send open message containing all saved chunks directly via event
            if (TryComp<ActorComponent>(args.Actor, out var actor))
            {
                var msg = new PlanetMapOpenMessage(GetNetEntity(uid),
                    new Dictionary<Vector2i, uint[]>(component.SavedChunks),
                    new Dictionary<Vector2i, uint[]>(component.SavedObjects),
                    new List<string>(component.ObjectPrototypes));
                RaiseNetworkEvent(msg, actor.PlayerSession);
            }
            //args.Handled = true;
        //}
    }

    // -----------------------------------------------------------------------
    // Event handlers
    // -----------------------------------------------------------------------

    private void OnMapOpened(PlanetMapOpenMessage msg, EntitySessionEventArgs args)
    {
        // Client just opened the map — send them all saved data
        if (!TryGetEntity(msg.MapEntity, out var mapEnt) ||
            !TryComp<PlanetMapComponent>(mapEnt, out var mapComp))
            return;

        var reply = new PlanetMapOpenMessage(msg.MapEntity,
            new Dictionary<Vector2i, uint[]>(mapComp.SavedChunks),
            new Dictionary<Vector2i, uint[]>(mapComp.SavedObjects),
            new List<string>(mapComp.ObjectPrototypes));
        RaiseNetworkEvent(reply, args.SenderSession);
    }

    private void OnScanRequest(PlanetMapScanRequestMessage msg, EntitySessionEventArgs args)
    {
        var player = args.SenderSession.AttachedEntity;
        if (player == null)
            return;

        if (!TryGetEntity(msg.MapEntity, out var mapEnt) ||
            !TryComp<PlanetMapComponent>(mapEnt, out var mapComp))
            return;

        var xform    = Transform(player.Value);
        var mapId    = xform.MapID;
        var playerPos = _xform.GetWorldPosition(player.Value);

        if (mapId == MapId.Nullspace)
            return;

        // Find the grid the player is standing on
        if (!_mapManager.TryFindGridAt(mapId, playerPos, out var gridUid, out var grid))
            return;

        var playerTile = _mapSys.LocalToTile(gridUid, grid, xform.Coordinates);

        // Biome component is needed to resolve virtual tiles
        TryComp<BiomeComponent>(gridUid, out var biome);

        var scanRadius = mapComp.ScanRadius;
        var newChunks  = new Dictionary<Vector2i, uint[]>();
        var newObjects = new Dictionary<Vector2i, uint[]>();

        // Iterate over the square scan area
        for (var dx = -scanRadius; dx <= scanRadius; dx++)
        {
            for (var dy = -scanRadius; dy <= scanRadius; dy++)
            {
                // Circular mask
                if (dx * dx + dy * dy > scanRadius * scanRadius)
                    continue;

                var tile        = playerTile + new Vector2i(dx, dy);
                var chunkOrigin = SharedPlanetMapSystem.GetChunkOrigin(tile);
                var relative    = SharedPlanetMapSystem.GetRelativeTile(tile, chunkOrigin);
                var index       = SharedPlanetMapSystem.GetTileIndex(relative);

                // Check LOS if required
                if (mapComp.RequireVisibility && !HasLineOfSight(gridUid, playerPos, mapId, tile, grid))
                    continue;

                // Determine the tile type and object and pack them
                var (packedTile, packedObj) = ClassifyTile(mapComp, gridUid, grid, tile, biome, mapId);

                // Get or create chunk buffer
                if (!newChunks.TryGetValue(chunkOrigin, out var chunkData))
                {
                    chunkData = new uint[SharedPlanetMapSystem.ArraySize];
                    newChunks[chunkOrigin] = chunkData;
                }
                if (!newObjects.TryGetValue(chunkOrigin, out var objData))
                {
                    objData = new uint[SharedPlanetMapSystem.ArraySize];
                    newObjects[chunkOrigin] = objData;
                }

                chunkData[index] = packedTile;
                objData[index] = packedObj;
            }
        }

        // Merge into persistent storage (higher-priority types win)
        MergeChunks(mapComp.SavedChunks, newChunks);
        MergeChunks(mapComp.SavedObjects, newObjects);

        Dirty(mapEnt.Value, mapComp);

        // Send newly scanned data to the client
        var response = new PlanetMapChunkDataMessage(msg.MapEntity, newChunks, newObjects, new List<string>(mapComp.ObjectPrototypes));
        RaiseNetworkEvent(response, args.SenderSession);
    }

    private void MergeChunks(Dictionary<Vector2i, uint[]> savedMap, Dictionary<Vector2i, uint[]> newMap)
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
                if (data[i] != 0)
                    saved[i] = data[i];
            }
        }
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Determines the visual type of a tile at the given grid indices.
    /// Priority: anchored entities (walls/trees/flowers) > actual tile type > biome virtual tile.
    /// </summary>
    private (uint tile, uint obj) ClassifyTile(PlanetMapComponent mapComp, EntityUid gridUid,
        MapGridComponent grid,
        Vector2i tile,
        BiomeComponent? biome,
        MapId mapId)
    {
        uint objData = 0;
        uint tileData = 0;

        // 1. Check anchored entities on this tile
        var anchored = _mapSys.GetAnchoredEntitiesEnumerator(gridUid, grid, tile);
        while (anchored.MoveNext(out var ent))
        {
            var meta = MetaData(ent.Value);
            if (meta.EntityPrototype != null)
            {
                // Display if: has hard physics OR has PlanetMapEntity tag
                var hasHardPhysics = TryComp<PhysicsComponent>(ent.Value, out var physics) && physics.Hard;
                var hasPlanetMapTag = _tag.HasTag(ent.Value, "PlanetMapEntity");

                if (hasHardPhysics || hasPlanetMapTag)
                {
                    var id = meta.EntityPrototype.ID;
                    var objIndex = mapComp.ObjectPrototypes.IndexOf(id);
                    if (objIndex < 0)
                    {
                        objIndex = mapComp.ObjectPrototypes.Count;
                        mapComp.ObjectPrototypes.Add(id);
                    }
                    objData = (uint)(objIndex + 1); // 0 is empty
                    break;
                }
            }
        }

        // 2. Check the actual grid tile
        if (_mapSys.TryGetTileRef(gridUid, grid, tile, out var tileRef) && !tileRef.Tile.IsEmpty)
        {
            tileData = (uint)tileRef.Tile.TypeId;
        }
        // 3. Fall back to biome virtual tile
        else if (biome != null && _biome.TryGetBiomeTile(gridUid, grid, tile, out var biomeTile) && biomeTile != null)
        {
            tileData = (uint)biomeTile.Value.TypeId;
        }

        return (tileData, objData);
    }



    /// <summary>
    /// Simple tile-based line-of-sight check using a Bresenham line walk.
    /// Returns false if any wall entity blocks the line between player and target tile.
    /// </summary>
    private bool HasLineOfSight(EntityUid gridUid,
        Vector2 playerWorldPos,
        MapId mapId,
        Vector2i targetTile,
        MapGridComponent grid)
    {
        // Convert target tile to world center
        var targetWorld = _mapSys.GridTileToWorld(gridUid, grid, targetTile);

        // Bresenham walk on tile coordinates
        var playerTile = _mapSys.LocalToTile(gridUid, grid, new EntityCoordinates(gridUid, playerWorldPos)); // close enough approximation for LOS


        int x0 = playerTile.X, y0 = playerTile.Y;
        int x1 = targetTile.X, y1 = targetTile.Y;

        int dx = Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
        int dy = -Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
        int err = dx + dy;

        while (true)
        {
            if (x0 == x1 && y0 == y1)
                break;

            // Check if this intermediate tile is blocked
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
            // Block line of sight if it's a hard physics object
            var hasHardPhysics = TryComp<PhysicsComponent>(ent.Value, out var physics) && physics.Hard;
            //var hasPlanetMapTag = _tag.HasTag(ent.Value, "PlanetMapEntity");

            if (hasHardPhysics)
                return true;
        }
        return false;
    }
}

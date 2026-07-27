using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Content.Server._CE.ZLevels.Core;
using Content.Server._CE.ZLevels.Core.Components;
using Content.Server.Body.Components;
using Content.Server.Body.Systems;
using Content.Server.GameTicking;
using Content.Server.Mind;
using Content.Server.Parallax;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._Nibiru.GameTicking.Rules;
using Content.Shared._Nibiru.SaveLoad;
using Content.Shared.Atmos;
using Content.Shared.Body.Components;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.SSDIndicator;
using Robust.Server.GameObjects;
using Robust.Shared.ContentPack;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._Nibiru.SaveLoad;

/// <summary>
/// Saves/loads a full round: maps (split by CE Z-level), living entities (players + NPCs)
/// and a JSON manifest tying it all together. Public API is intentionally small:
/// <see cref="SaveRound"/>, <see cref="RequestLoad"/>, <see cref="LoadSavedMaps"/>,
/// <see cref="TryLoadSavedPlayer"/>.
/// </summary>
public sealed class NibiruRoundSaveSystem : EntitySystem
{
    [Dependency] private readonly IResourceManager _res = default!;
    [Dependency] private readonly GameTicker _ticker = default!;
    [Dependency] private readonly IMapManager _map = default!;
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly CEZLevelsSystem _zLevels = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly RespiratorSystem _respirator = default!;
    [Dependency] private readonly BiomeSystem _biome = default!;
    [Dependency] private readonly MindSystem _mind = default!;

    /// <summary>Name of the save that should be loaded on the next round start, if any.</summary>
    public string? SaveToLoad { get; private set; }

    /// <summary>
    /// What the system is currently doing. Used both to reject overlapping save/load calls
    /// and to tell <see cref="OnIsSerializable"/> what to do with mob entities.
    /// </summary>
    private enum Phase
    {
        Idle,
        SavingMaps,
        SavingEntities,
        Loading
    }

    private Phase _state = Phase.Idle;

    // Per-type reflection cache for resetting [AutoPausedField] members after load.
    private static readonly Dictionary<Type, (FieldInfo[] Fields, PropertyInfo[] Props)> AutoPausedCache = new();

    public override void Initialize()
    {
        base.Initialize();
        _mapLoader.OnIsSerializable += OnIsSerializable;
        SubscribeNetworkEvent<RequestSavedCharacterMessage>(OnSaveCharacterRequest);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _mapLoader.OnIsSerializable -= OnIsSerializable;
    }

    #region Client requests

    private void OnSaveCharacterRequest(RequestSavedCharacterMessage msg, EntitySessionEventArgs args)
    {
        SendSavedCharacterList(args.SenderSession);
    }

    private void SendSavedCharacterList(ICommonSession session)
    {
        var userId = session.UserId.ToString();
        var names = new List<string>();

        var query = EntityQueryEnumerator<NibiruSavedPlayerComponent, MetaDataComponent>();
        while (query.MoveNext(out _, out var saved, out var meta))
        {
            if (saved.UserId == userId && !string.IsNullOrWhiteSpace(meta.EntityName))
                names.Add(meta.EntityName);
        }

        RaiseNetworkEvent(new SavedCharacterAvailableMessage(names), session.Channel);
    }

    #endregion

    #region Reconnect

    /// <summary>
    /// Reconnects a player to their saved entity (late-join reconnection via console command).
    /// The player is assumed to already be in-game as an observer; this does not call
    /// PlayerJoinGame, it just transfers the existing mind onto the saved entity.
    /// </summary>
    public void TryLoadSavedPlayer(ICommonSession player, string? targetCharacter = null)
    {
        if (player.Status == SessionStatus.Disconnected)
            return;

        var userId = player.UserId.ToString();
        var query = EntityQueryEnumerator<NibiruSavedPlayerComponent, MetaDataComponent>();

        while (query.MoveNext(out var uid, out var saved, out var meta))
        {
            if (saved.UserId != userId)
                continue;
            if (targetCharacter != null && meta.EntityName != targetCharacter)
                continue;
            if (!Exists(uid))
                continue;

            // Remove the marker immediately so a duplicate call can't reconnect twice.
            RemComp<NibiruSavedPlayerComponent>(uid);

            Log.Info($"[SaveLoad] Reconnecting {player.Name} to saved entity {uid} ({meta.EntityName}).");

            // Always create a fresh mind instead of reusing GetMind(): after WipeAllMinds the
            // old ContentData.Mind reference can be stale even though UserMinds is empty,
            // which trips a DebugAssert. CreateMind cleans up any stale mind via SetUserId.
            var mindId = _mind.CreateMind(player.UserId, meta.EntityName);
            _mind.TransferTo(mindId, uid);
            RemComp<SSDIndicatorComponent>(uid);

            Log.Info($"[SaveLoad] Player {player.Name} transferred to saved entity {uid}.");
            return;
        }

        Log.Warning($"[SaveLoad] No saved entity found for {player.Name} (character={targetCharacter}).");
    }

    #endregion

    #region Serialization filter

    private void OnIsSerializable(Entity<MetaDataComponent> ent, ref bool serializable)
    {
        switch (_state)
        {
            case Phase.SavingEntities:
                // Saving a single player/NPC tree: everything in it must serialize, including
                // organs/items that don't have MobStateComponent themselves.
                serializable = true;
                break;

            case Phase.SavingMaps:
                // Saving a map: mobs are saved separately as entity files, so skip them here.
                if (HasComp<MobStateComponent>(ent))
                    serializable = false;
                break;
        }
    }

    #endregion

    #region Save

    public void SaveRound(string saveName)
    {
        if (_state != Phase.Idle)
        {
            Log.Error($"[SaveLoad] Cannot start save '{saveName}': operation already in progress ({_state}).");
            return;
        }

        Log.Info($"[SaveLoad] Saving round '{saveName}'...");

        try
        {
            var basePath = new ResPath($"/Saves/{saveName}");
            PrepareSaveDirectory(basePath);

            var manifest = new RoundSaveManifest
            {
                PresetId = _ticker.CurrentPreset?.ID ?? "sandbox"
            };
            Log.Info($"[SaveLoad] Preset: '{manifest.PresetId}'.");

            _state = Phase.SavingMaps;
            SaveMaps(basePath, manifest);
            Log.Info($"[SaveLoad] Saved maps: {manifest.Maps.Count}.");

            _state = Phase.SavingEntities;
            SaveLivingEntities(basePath, manifest);

            WriteManifest(basePath, manifest);

            Log.Info($"[SaveLoad] Save '{saveName}' complete. Maps: {manifest.Maps.Count}, Players: {manifest.Players.Count}.");
        }
        catch (Exception e)
        {
            Log.Error($"[SaveLoad] Save '{saveName}' failed with an exception: {e}");
        }
        finally
        {
            _state = Phase.Idle;
        }
    }

    private void PrepareSaveDirectory(ResPath basePath)
    {
        // Wipe any previous save with this name so we never mix files from two saves.
        if (_res.UserData.Exists(basePath))
            _res.UserData.Delete(basePath);

        _res.UserData.CreateDir(basePath);
    }

    private void SaveMaps(ResPath basePath, RoundSaveManifest manifest)
    {
        var mapFolder = basePath / "Maps";
        _res.UserData.CreateDir(mapFolder);

        var networkIds = BuildNetworkIndex();
        var allMapIds = _map.GetAllMapIds().ToList();
        Log.Info($"[SaveLoad] All maps: {allMapIds.Count}.");

        foreach (var mapId in allMapIds)
        {
            if (mapId == MapId.Nullspace)
                continue;

            var mapUid = _map.GetMapEntityId(mapId);
            if (!Exists(mapUid))
            {
                Log.Warning($"[SaveLoad] Map {mapId}: entity doesn't exist, skipping.");
                continue;
            }

            if (!TryGetZLevel(mapUid, out var zLevel))
            {
                Log.Warning($"[SaveLoad] Map {mapId} '{MetaData(mapUid).EntityName}': cannot determine Z-level, skipping.");
                continue;
            }

            var networkId = -1;
            if (_zLevels.TryGetZNetwork(mapUid, out var zNetwork))
                networkId = networkIds.GetValueOrDefault(zNetwork.Value.Owner, -1);

            SaveSingleMap(mapId, mapUid, zLevel, networkId, mapFolder, manifest);
        }
    }

    private Dictionary<EntityUid, int> BuildNetworkIndex()
    {
        var ids = new Dictionary<EntityUid, int>();
        var next = 0;

        var query = EntityQueryEnumerator<CEZLevelsNetworkComponent>();
        while (query.MoveNext(out var uid, out _))
            ids[uid] = next++;

        Log.Info($"[SaveLoad] Found Z-networks: {ids.Count}.");
        return ids;
    }

    /// <summary>
    /// CEZLevelMapComponent is the normal source of truth for a map's depth, but it's
    /// [UnsavedComponent] and won't exist right after a fresh round start, so we fall back
    /// to the survival rule's map references, then to a fixed map name.
    /// </summary>
    private bool TryGetZLevel(EntityUid mapUid, out int zLevel)
    {
        if (TryComp<CEZLevelMapComponent>(mapUid, out var comp))
        {
            zLevel = comp.Depth;
            return true;
        }

        foreach (var rule in EntityQuery<NibiruSurvivalRuleComponent>())
        {
            if (rule.WorldMap == mapUid)
            {
                zLevel = 0;
                return true;
            }

            if (rule.CaveMap == mapUid)
            {
                zLevel = -1;
                return true;
            }
        }

        zLevel = MetaData(mapUid).EntityName switch
        {
            "level -1" => -1,
            "level 0" => 0,
            "level 1" => 1,
            "level 2" => 2,
            _ => int.MinValue
        };

        return zLevel != int.MinValue;
    }

    private void SaveSingleMap(MapId mapId, EntityUid mapUid, int zLevel, int networkId, ResPath mapFolder, RoundSaveManifest manifest)
    {
        var mapFile = mapFolder / $"map_{(int)mapId}.yml";
        var mapName = MetaData(mapUid).EntityName;
        Log.Info($"[SaveLoad] Saving map {mapId} '{mapName}' (z={zLevel}) -> {mapFile}");

        // Biome runtime state (loaded chunks/entities/decals) can't round-trip as-is:
        // entity/decal references would be stale on reload, but the chunk list itself
        // must be kept so the biome doesn't regenerate from scratch after loading.
        var biomeSnap = _biome.PrepareMapForSave(mapUid);
        bool saved;
        try
        {
            saved = _mapLoader.TrySaveMap(mapId, mapFile);
        }
        finally
        {
            if (biomeSnap.HasValue)
                _biome.RestoreMapAfterSave(mapUid, biomeSnap.Value);
        }

        if (!saved)
        {
            Log.Error($"[SaveLoad] Failed to save map {mapId} '{mapName}'!");
            return;
        }

        manifest.Maps.Add(new MapSaveData
        {
            MapId = (int)mapId,
            ZLevel = zLevel,
            NetworkId = networkId,
            MapFile = mapFile.ToString()
        });
        Log.Info($"[SaveLoad] Map {mapId} saved.");
    }

    private void SaveLivingEntities(ResPath basePath, RoundSaveManifest manifest)
    {
        Log.Info("[SaveLoad] Saving living entities...");

        var playerFolder = basePath / "Players";
        _res.UserData.CreateDir(playerFolder);

        var npcFolder = basePath / "Mobs";
        _res.UserData.CreateDir(npcFolder);

        var (players, npcs) = CollectLivingEntities();
        Log.Info($"[SaveLoad] Players to save: {players.Count}, NPCs: {npcs.Count}.");

        // Engine serialization checks EntityPrototype.MapSavable before calling
        // OnIsSerializable — so the delegate can't override it. Temporarily enable
        // MapSavable on all mob prototypes so their full entity tree serializes.
        var impactedProtos = new HashSet<EntityPrototype>();
        foreach (var uid in players.Concat(npcs))
        {
            var meta = MetaData(uid);
            if (meta.EntityPrototype is { MapSavable: false } proto)
                impactedProtos.Add(proto);
        }
        foreach (var proto in impactedProtos)
            proto.MapSavable = true;

        var saveOpts = new SerializationOptions { ErrorOnOrphan = false };

        try
        {
            foreach (var uid in players)
                SavePlayer(uid, playerFolder, saveOpts, manifest);

            if (npcs.Count > 0)
                SaveNpcs(npcs, npcFolder, saveOpts, manifest);
        }
        finally
        {
            foreach (var proto in impactedProtos)
                proto.MapSavable = false;
        }
    }

    /// <summary>
    /// Finds every mob in the world, tags it with where it is (so it can be put back in the
    /// right place after loading) and splits it into players vs. NPCs.
    /// </summary>
    private (List<EntityUid> Players, HashSet<EntityUid> Npcs) CollectLivingEntities()
    {
        var players = new List<EntityUid>();
        var npcs = new HashSet<EntityUid>();

        var query = EntityQueryEnumerator<MobStateComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            var parent = EnsureComp<NibiruSaveParentComponent>(uid);
            parent.MapId = (int)xform.MapID;
            parent.Position = _xform.GetWorldPosition(xform);
            parent.Rotation = _xform.GetWorldRotation(xform);

            if (TryComp<MindContainerComponent>(uid, out var mind) && mind.HasMind)
                players.Add(uid);
            else
                npcs.Add(uid);
        }

        return (players, npcs);
    }

    private void SavePlayer(EntityUid uid, ResPath playerFolder, SerializationOptions opts, RoundSaveManifest manifest)
    {
        var meta = Comp<MetaDataComponent>(uid);
        var userId = GetPlayerUserId(uid);
        var file = playerFolder / $"{userId}.yml";

        try
        {
            Log.Debug($"[SaveLoad] Saving player '{meta.EntityName}' ({userId}) -> {file}");

            if (_mapLoader.TrySaveEntity(uid, file, opts))
            {
                manifest.Players.Add(new PlayerSaveData
                {
                    UserId = userId,
                    EntityName = meta.EntityName,
                    File = file.ToString()
                });
                Log.Info($"[SaveLoad] Player '{meta.EntityName}' saved.");
            }
            else
            {
                Log.Error($"[SaveLoad] Failed to save player '{meta.EntityName}' ({userId})!");
            }
        }
        finally
        {
            RemComp<NibiruSaveParentComponent>(uid);
        }
    }

    private void SaveNpcs(HashSet<EntityUid> npcs, ResPath npcFolder, SerializationOptions opts, RoundSaveManifest manifest)
    {
        var file = npcFolder / "npcs.yml";

        try
        {
            Log.Debug($"[SaveLoad] Saving NPCs ({npcs.Count}) -> {file}");

            if (_mapLoader.TrySaveGeneric(npcs, file, out _, opts))
            {
                manifest.NpcFile = file.ToString();
                Log.Info("[SaveLoad] NPCs saved.");
            }
            else
            {
                Log.Error("[SaveLoad] Failed to save NPCs!");
            }
        }
        finally
        {
            foreach (var uid in npcs)
                RemComp<NibiruSaveParentComponent>(uid);
        }
    }

    private void WriteManifest(ResPath basePath, RoundSaveManifest manifest)
    {
        var path = basePath / "manifest.json";
        var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });

        using var stream = _res.UserData.OpenWriteText(path);
        stream.Write(json);
    }

    /// <summary>
    /// Extracts a stable UserId for a player entity. Prefers the live session (connected
    /// player), falls back to the mind's stored UserId (disconnected), then to a synthetic
    /// id so the save never fails outright.
    /// </summary>
    private string GetPlayerUserId(EntityUid uid)
    {
        if (TryComp<ActorComponent>(uid, out var actor))
            return actor.PlayerSession.UserId.ToString();

        if (TryComp<MindContainerComponent>(uid, out var mindContainer)
            && mindContainer.HasMind
            && _mind.TryGetMind(uid, out _, out var mindComp))
        {
            return mindComp.UserId?.ToString() ?? $"entity_{uid}";
        }

        return $"entity_{uid}";
    }

    #endregion

    #region Load

    public void RequestLoad(string saveName)
    {
        if (_state != Phase.Idle)
        {
            Log.Error($"[SaveLoad] Cannot request load of '{saveName}': operation already in progress ({_state}).");
            return;
        }

        var manifestPath = new ResPath($"/Saves/{saveName}") / "manifest.json";
        if (!_res.UserData.Exists(manifestPath))
        {
            Log.Error($"[SaveLoad] Save '{saveName}' not found (missing manifest).");
            return;
        }

        var manifest = ReadManifest(manifestPath);
        if (manifest == null || string.IsNullOrEmpty(manifest.PresetId))
        {
            Log.Error($"[SaveLoad] Save '{saveName}' has an invalid manifest.");
            return;
        }

        SaveToLoad = saveName;
        _ticker.SetGamePreset(manifest.PresetId, force: false);
        _ticker.RestartRound();
    }

    public void ClearLoad()
    {
        SaveToLoad = null;
    }

    /// <summary>
    /// Actually loads the maps/entities for <see cref="SaveToLoad"/>. Meant to be called once
    /// during round setup, after <see cref="RequestLoad"/> has restarted the round.
    /// </summary>
    public bool LoadSavedMaps(out EntityUid loadedCave, out EntityUid loadedWorld, out EntityUid loadedSky1, out EntityUid loadedSky2)
    {
        loadedCave = EntityUid.Invalid;
        loadedWorld = EntityUid.Invalid;
        loadedSky1 = EntityUid.Invalid;
        loadedSky2 = EntityUid.Invalid;

        if (SaveToLoad is not { } saveName)
            return false;

        if (_state != Phase.Idle)
        {
            Log.Error($"[SaveLoad] Cannot load '{saveName}': operation already in progress ({_state}).");
            return false;
        }

        _state = Phase.Loading;
        Log.Info($"[SaveLoad] Loading save '{saveName}'...");

        try
        {
            var basePath = new ResPath($"/Saves/{saveName}");
            var manifestPath = basePath / "manifest.json";

            if (!_res.UserData.Exists(manifestPath))
            {
                Log.Error($"[SaveLoad] Manifest not found: {manifestPath}!");
                return false;
            }

            var manifest = ReadManifest(manifestPath);
            if (manifest == null)
            {
                Log.Error("[SaveLoad] Failed to deserialize manifest!");
                return false;
            }

            Log.Info($"[SaveLoad] Manifest: {manifest.Maps.Count} maps, {manifest.Players.Count} players, NpcFile={manifest.NpcFile ?? "none"}.");

            var oldIdToNewUid = LoadMaps(manifest, out var mapsByZLevel);
            RestoreZNetworks(manifest, oldIdToNewUid);
            LoadLivingEntities(manifest, oldIdToNewUid);

            loadedCave = mapsByZLevel.GetValueOrDefault(-1, EntityUid.Invalid);
            loadedWorld = mapsByZLevel.GetValueOrDefault(0, EntityUid.Invalid);
            loadedSky1 = mapsByZLevel.GetValueOrDefault(1, EntityUid.Invalid);
            loadedSky2 = mapsByZLevel.GetValueOrDefault(2, EntityUid.Invalid);

            return true;
        }
        catch (Exception e)
        {
            Log.Error($"[SaveLoad] Load '{saveName}' failed with an exception: {e}");
            return false;
        }
        finally
        {
            _state = Phase.Idle;
        }
    }

    private RoundSaveManifest? ReadManifest(ResPath path)
    {
        using var stream = _res.UserData.OpenRead(path);
        return JsonSerializer.Deserialize<RoundSaveManifest>(stream);
    }

    private Dictionary<int, EntityUid> LoadMaps(RoundSaveManifest manifest, out Dictionary<int, EntityUid> mapsByZLevel)
    {
        var oldIdToNewUid = new Dictionary<int, EntityUid>();
        mapsByZLevel = new Dictionary<int, EntityUid>();

        foreach (var mapData in manifest.Maps)
        {
            Log.Info($"[SaveLoad] Loading map z={mapData.ZLevel}, file={mapData.MapFile} (oldMapId={mapData.MapId})...");

            if (!_mapLoader.TryLoadMap(new ResPath(mapData.MapFile), out var newMap, out _))
            {
                Log.Error($"[SaveLoad] Failed to load map '{mapData.MapFile}' (z={mapData.ZLevel})!");
                continue;
            }

            var mapUid = newMap.Value.Owner;
            oldIdToNewUid[mapData.MapId] = mapUid;
            mapsByZLevel[mapData.ZLevel] = mapUid;

            Log.Info($"[SaveLoad] Map z={mapData.ZLevel} loaded: newUid={mapUid} (oldId={mapData.MapId}).");
        }

        return oldIdToNewUid;
    }

    private void RestoreZNetworks(RoundSaveManifest manifest, Dictionary<int, EntityUid> oldIdToNewUid)
    {
        var byNetwork = new Dictionary<int, Dictionary<EntityUid, int>>();

        foreach (var mapData in manifest.Maps)
        {
            if (mapData.NetworkId == -1 || !oldIdToNewUid.TryGetValue(mapData.MapId, out var mapUid))
                continue;

            if (!byNetwork.TryGetValue(mapData.NetworkId, out var maps))
                byNetwork[mapData.NetworkId] = maps = new Dictionary<EntityUid, int>();

            maps[mapUid] = mapData.ZLevel;
        }

        foreach (var maps in byNetwork.Values)
        {
            var network = _zLevels.CreateZNetwork();
            _zLevels.TryAddMapsIntoZNetwork(network, maps);
            Log.Info($"[SaveLoad] Created Z-network with {maps.Count} maps.");
        }
    }

    private void LoadLivingEntities(RoundSaveManifest manifest, Dictionary<int, EntityUid> oldIdToNewUid)
    {
        foreach (var player in manifest.Players)
            LoadEntityFile(player.File, isPlayerFile: true, oldIdToNewUid);

        if (!string.IsNullOrEmpty(manifest.NpcFile))
            LoadEntityFile(manifest.NpcFile, isPlayerFile: false, oldIdToNewUid);
    }

    private void LoadEntityFile(string file, bool isPlayerFile, Dictionary<int, EntityUid> oldIdToNewUid)
    {
        var resPath = new ResPath(file);
        if (!_res.UserData.Exists(resPath))
        {
            Log.Error($"[SaveLoad] Entity file not found: {file}");
            return;
        }

        Log.Info($"[SaveLoad] Loading entity file: {file}");

        if (!_mapLoader.TryLoadGeneric(resPath, out var result))
        {
            Log.Error($"[SaveLoad] Failed to load entity file: {file}");
            return;
        }

        var userId = isPlayerFile ? GetFileNameWithoutExtension(resPath) : string.Empty;
        Log.Debug($"[SaveLoad] Loaded entities: {result.Entities.Count} + orphans: {result.Orphans.Count}");

        foreach (var uid in result.Entities.Concat(result.Orphans))
            RestoreLoadedEntity(uid, isPlayerFile, userId, oldIdToNewUid);
    }

    private void RestoreLoadedEntity(EntityUid uid, bool isPlayerFile, string userId, Dictionary<int, EntityUid> oldIdToNewUid)
    {
        var isRoot = HasComp<NibiruSaveParentComponent>(uid);

        if (isPlayerFile && isRoot)
            MarkAsSavedPlayer(uid, userId);

        // Position first — physiology reset below needs the entity's final atmosphere.
        RepositionEntity(uid, oldIdToNewUid);
        ResetPhysiology(uid);
        ResetAllAutoPausedFields(uid);
    }

    private void MarkAsSavedPlayer(EntityUid uid, string userId)
    {
        Log.Debug($"[SaveLoad] Restoring player uid={uid}, userId={userId}");

        var saved = EnsureComp<NibiruSavedPlayerComponent>(uid);
        saved.UserId = userId;

        var ssd = EnsureComp<SSDIndicatorComponent>(uid);
        ssd.IsSSD = true;
        EnsureComp<NibiruNoSSDSleepComponent>(uid);
    }

    private void RepositionEntity(EntityUid uid, Dictionary<int, EntityUid> oldIdToNewUid)
    {
        if (!TryComp<NibiruSaveParentComponent>(uid, out var parent))
            return;

        try
        {
            if (!oldIdToNewUid.TryGetValue(parent.MapId, out var targetMap))
            {
                Log.Error($"[SaveLoad] uid={uid}: no mapping for saved mapId={parent.MapId}! Available: [{string.Join(", ", oldIdToNewUid.Keys)}]");
                return;
            }

            _xform.SetParent(uid, targetMap);
            _xform.SetLocalPositionRotation(uid, parent.Position, parent.Rotation);

            if (TryComp<MapComponent>(targetMap, out var mapComp)
                && _map.TryFindGridAt(mapComp.MapId, parent.Position, out var gridUid, out _))
            {
                _xform.SetParent(uid, gridUid);
            }
        }
        finally
        {
            RemComp<NibiruSaveParentComponent>(uid);
        }
    }

    /// <summary>Resets breathing gas mix and respirator/SSD timers so the entity doesn't wake
    /// up mid-suffocation or with a wildly out-of-date SSD timer after being restored.</summary>
    private void ResetPhysiology(EntityUid uid)
    {
        if (TryComp<RespiratorComponent>(uid, out var respirator))
        {
            _respirator.UpdateSaturation(uid, respirator.MaxSaturation - respirator.Saturation, respirator);
            _respirator.ResetTimer((uid, respirator));
        }

        if (TryComp<LungComponent>(uid, out var lung))
        {
            lung.Air.SetMoles(Gas.Oxygen, 1.5f);
            lung.Air.SetMoles(Gas.Nitrogen, 5.6f);
        }

        if (TryComp<SSDIndicatorComponent>(uid, out var ssd))
            ssd.NextUpdate = _gameTiming.CurTime + ssd.UpdateInterval;
    }

    private static string GetFileNameWithoutExtension(ResPath path)
    {
        return Path.GetFileNameWithoutExtension(path.Filename);
    }

    /// <summary>
    /// Resets every [AutoPausedField] TimeSpan field/property on an entity's components to the
    /// current time. Without this, timestamps serialized from before the save (e.g. "next
    /// breath at T") are in the past on load, and systems think a huge interval has elapsed —
    /// causing rapid-fire metabolism/breathing/etc. right after loading.
    /// </summary>
    private void ResetAllAutoPausedFields(EntityUid uid)
    {
        var curTime = _gameTiming.CurTime;

        foreach (var component in EntityManager.GetComponents(uid))
        {
            var compType = component.GetType();

            if (!AutoPausedCache.TryGetValue(compType, out var cache))
            {
                var fields = compType
                    .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(f => Attribute.IsDefined(f, typeof(AutoPausedFieldAttribute)))
                    .ToArray();
                var props = compType
                    .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(p => Attribute.IsDefined(p, typeof(AutoPausedFieldAttribute)))
                    .ToArray();

                cache = (fields, props);
                AutoPausedCache[compType] = cache;
            }

            foreach (var field in cache.Fields)
                ResetIfStale(field.FieldType, () => field.GetValue(component), v => field.SetValue(component, v), curTime);

            foreach (var prop in cache.Props)
                ResetIfStale(prop.PropertyType, () => prop.GetValue(component), v => prop.SetValue(component, v), curTime);
        }
    }

    private static void ResetIfStale(Type memberType, Func<object?> getter, Action<object?> setter, TimeSpan curTime)
    {
        if (memberType == typeof(TimeSpan))
        {
            var val = (TimeSpan)getter()!;
            if (val == TimeSpan.Zero || val < curTime)
                setter(curTime);
        }
        else if (memberType == typeof(TimeSpan?))
        {
            var val = (TimeSpan?)getter();
            if (val != null && (val.Value == TimeSpan.Zero || val.Value < curTime))
                setter(curTime);
        }
    }

    #endregion
}

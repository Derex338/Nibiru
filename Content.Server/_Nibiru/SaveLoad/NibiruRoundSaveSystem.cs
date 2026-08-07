using Content.Server._CE.ZLevels.Core;
using Content.Server._CE.ZLevels.Core.Components;
using Content.Server.Body.Components;
using Content.Server.Body.Systems;
using Content.Server.Database;
using Content.Server.GameTicking;
using Content.Server.Mind;
using Content.Server.Parallax;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._Nibiru.GameTicking.Rules;
using Content.Shared._Nibiru.SaveLoad;
using Content.Shared.Atmos;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.GameTicking;
using Content.Shared.Ghost;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.SSDIndicator;
using Robust.Server.GameObjects;
using Robust.Server.Player;
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
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace Content.Server._Nibiru.SaveLoad;

/// <summary>
/// Handles unified round save and load operations for Nibiru.
/// All map grids, structures, items, NPCs, and player character bodies are saved directly inside Z-level map files.
/// </summary>
public sealed partial class NibiruRoundSaveSystem : EntitySystem
{
[Dependency] private IPlayerManager _playerManager = default!;
[Dependency] private IResourceManager _res = default!;
[Dependency] private GameTicker _ticker = default!;
[Dependency] private IMapManager _map = default!;
[Dependency] private MapLoaderSystem _mapLoader = default!;
[Dependency] private CEZLevelsSystem _zLevels = default!;
[Dependency] private IGameTiming _gameTiming = default!;
[Dependency] private RespiratorSystem _respirator = default!;
[Dependency] private BiomeSystem _biome = default!;
[Dependency] private MindSystem _mind = default!;

    /// <summary>
    /// Name of the save file to load on round restart, if any.
    /// </summary>
    public string? SaveToLoad { get; private set; }

    private bool _isSaving;
    private static readonly Dictionary<Type, (FieldInfo[] Fields, PropertyInfo[] Props)> AutoPausedCache = new();

    public override void Initialize()
    {
        base.Initialize();
        _mapLoader.OnIsSerializable += OnIsSerializable;
        SubscribeNetworkEvent<RequestSavedCharacterMessage>(OnSaveCharacterRequest);
        //SubscribeLocalEvent<PlayerBeforeSpawnEvent>(OnPlayerBeforeSpawnEvent);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _mapLoader.OnIsSerializable -= OnIsSerializable;
    }

    private void OnIsSerializable(Entity<MetaDataComponent> ent, ref bool serializable)
    {
        if (_isSaving)
        {
            // Never save observer / admin ghosts into map files
            if (HasComp<GhostComponent>(ent))
            {
                serializable = false;
                return;
            }

            // Force all mobs, organs, clothing, inventory items, and structures on maps to serialize
            serializable = true;
        }
    }

    #region Client Requests

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

    #region Player Reconnection

    /// <summary>
    /// Reconnects a player to their saved character entity on a loaded map.
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

            RemComp<NibiruSavedPlayerComponent>(uid);
            RemComp<NibiruNoSSDSleepComponent>(uid);

            Log.Info($"[SaveLoad] Reconnecting player {player.Name} to saved entity {uid} ({meta.EntityName}).");

            // Получаем старый Mind напрямую, обходя TryGetMind который может быть рассинхронизирован
            /*var oldMindId = player;
            if (oldMindId != null)
            {
                Log.Info($"[SaveLoad] Wiping old mind {oldMindId} for player {player.Name}");
                _mind.WipeMind(oldMindId);
            }*/

            // Создаём новый Mind с именем персонажа
            var newMind = _mind.CreateMind(player.UserId, meta.EntityName);

            // Устанавливаем UserId явно
            _mind.SetUserId(newMind, player.UserId);

            // Переносим разум в сущность
            _mind.TransferTo(newMind, uid);

            if (TryComp<SSDIndicatorComponent>(uid, out var ssd))
            {
                ssd.IsSSD = false;
                Dirty(uid, ssd);
            }

            Log.Info($"[SaveLoad] Player {player.Name} successfully transferred to saved entity {uid}.");
            return;
        }

        Log.Warning($"[SaveLoad] No saved entity found for player {player.Name} (target={targetCharacter ?? "any"}).");
    }

    private void OnPlayerBeforeSpawnEvent(PlayerBeforeSpawnEvent ev)
    {
        if (ev.Handled)
            return;

        TryLoadSavedPlayer(ev.Player, ev.Profile.Name);

        ev.Handled = true;
    }

    #endregion

    #region Save Operation

    public void SaveRound(string saveName)
    {
        Log.Info($"[SaveLoad] Starting save operation for round '{saveName}'...");

        var impactedProtos = new HashSet<EntityPrototype>();

        try
        {
            _isSaving = true;

            var basePath = new ResPath($"/Saves/{saveName}");
            PrepareSaveDirectory(basePath);

            var manifest = new RoundSaveManifest
            {
                PresetId = _ticker.CurrentPreset?.ID ?? "sandbox",
                SavedAt = DateTime.UtcNow.ToString("o")
            };

            TagPlayerEntities(manifest);

            // Temporarily enable MapSavable on all mob prototypes so MapLoaderSystem saves them into map files
            var mobQuery = EntityQueryEnumerator<MobStateComponent, MetaDataComponent>();
            while (mobQuery.MoveNext(out _, out var meta))
            {
                if (meta.EntityPrototype is { MapSavable: false } proto)
                {
                    proto.MapSavable = true;
                    impactedProtos.Add(proto);
                }
            }

            // Also enable MapSavable for all solution entities (blood, lung air, stomach contents, etc.)
            // They are ContainedSolutionComponent entities living inside the mob's 'solutions' container.
            // EntitySerializer checks MapSavable BEFORE our OnIsSerializable hook, so we must patch the prototype.
            var containedSolutionQuery = EntityQueryEnumerator<ContainedSolutionComponent, MetaDataComponent>();
            while (containedSolutionQuery.MoveNext(out _, out _, out var sMeta))
            {
                if (sMeta.EntityPrototype is { MapSavable: false } sproto)
                {
                    sproto.MapSavable = true;
                    impactedProtos.Add(sproto);
                }
            }

            var solutionQuery = EntityQueryEnumerator<SolutionComponent, MetaDataComponent>();
            while (solutionQuery.MoveNext(out _, out _, out var sMeta))
            {
                if (sMeta.EntityPrototype is { MapSavable: false } sproto)
                {
                    sproto.MapSavable = true;
                    impactedProtos.Add(sproto);
                }
            }

            SaveMaps(basePath, manifest);
            WriteManifest(basePath, manifest);

            Log.Info($"[SaveLoad] Round '{saveName}' saved successfully! Saved maps: {manifest.Maps.Count}, Players tagged: {manifest.Players.Count}.");
        }
        catch (Exception e)
        {
            Log.Error($"[SaveLoad] Failed to save round '{saveName}': {e}");
        }
        finally
        {
            _isSaving = false;
            foreach (var proto in impactedProtos)
            {
                proto.MapSavable = false;
            }
        }
    }

    private void PrepareSaveDirectory(ResPath basePath)
    {
        if (_res.UserData.Exists(basePath))
            _res.UserData.Delete(basePath);

        _res.UserData.CreateDir(basePath);
    }

    private void TagPlayerEntities(RoundSaveManifest manifest)
    {
        var query = EntityQueryEnumerator<MobStateComponent, MetaDataComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var meta, out var xform))
        {
            string? userId = GetPlayerUserId(uid);
            if (string.IsNullOrEmpty(userId))
                continue;

            var savedComp = EnsureComp<NibiruSavedPlayerComponent>(uid);
            savedComp.UserId = userId;
            savedComp.CharacterName = meta.EntityName;

            EnsureComp<SSDIndicatorComponent>(uid);
            EnsureComp<NibiruNoSSDSleepComponent>(uid);

            manifest.Players.Add(new PlayerSaveData
            {
                UserId = userId,
                CharacterName = meta.EntityName,
                MapId = (int)xform.MapID
            });
        }
    }

    private string? GetPlayerUserId(EntityUid uid)
    {
        if (TryComp<ActorComponent>(uid, out var actor))
            return actor.PlayerSession.UserId.ToString();

        if (_playerManager.TryGetSessionByEntity(uid, out var session))
            return session.UserId.ToString();

        if (TryComp<NibiruSavedPlayerComponent>(uid, out var saved) && !string.IsNullOrEmpty(saved.UserId))
            return saved.UserId;

        return null;
    }

    private void SaveMaps(ResPath basePath, RoundSaveManifest manifest)
    {
        var mapFolder = basePath / "Maps";
        _res.UserData.CreateDir(mapFolder);

        var networkIds = BuildNetworkIndex();
        var allMapIds = _map.GetAllMapIds().ToList();

        foreach (var mapId in allMapIds)
        {
            if (mapId == MapId.Nullspace)
                continue;

            var mapUid = _map.GetMapEntityId(mapId);
            if (!Exists(mapUid))
                continue;

            if (!TryGetZLevel(mapUid, out var zLevel))
                continue;

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

        return ids;
    }

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
    }

    private void WriteManifest(ResPath basePath, RoundSaveManifest manifest)
    {
        var path = basePath / "manifest.json";
        var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });

        using var stream = _res.UserData.OpenWriteText(path);
        stream.Write(json);
    }

    #endregion

    #region Load Operation

    public void RequestLoad(string saveName)
    {
        var manifestPath = new ResPath($"/Saves/{saveName}") / "manifest.json";
        if (!_res.UserData.Exists(manifestPath))
        {
            Log.Error($"[SaveLoad] Save '{saveName}' not found (missing manifest: {manifestPath}).");
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

    public bool LoadSavedMaps(out EntityUid loadedCave, out EntityUid loadedWorld, out List<EntityUid> skyMaps)
    {
        loadedCave = EntityUid.Invalid;
        loadedWorld = EntityUid.Invalid;
        skyMaps = new();

        if (SaveToLoad is not { } saveName)
            return false;

        Log.Info($"[SaveLoad] Loading round save '{saveName}'...");

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

            var oldIdToNewUid = LoadMaps(manifest, out var mapsByZLevel);
            RestoreZNetworks(manifest, oldIdToNewUid);
            PostLoadEntityCleanup();

            loadedCave = mapsByZLevel.GetValueOrDefault(-1, EntityUid.Invalid);
            loadedWorld = mapsByZLevel.GetValueOrDefault(0, EntityUid.Invalid);
            skyMaps = mapsByZLevel
                .Where(kvp => kvp.Key > 0)
                .OrderBy(kvp => kvp.Key)
                .Select(kvp => kvp.Value)
                .ToList();

            Log.Info($"[SaveLoad] Successfully loaded save '{saveName}'. Cave: {loadedCave}, World: {loadedWorld}, Sky maps: {skyMaps.Count}.");
            return true;
        }
        catch (Exception e)
        {
            Log.Error($"[SaveLoad] Exception during load of save '{saveName}': {e}");
            return false;
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

        var opts = DeserializationOptions.Default with { InitializeMaps = true };

        foreach (var mapData in manifest.Maps)
        {
            var resPath = new ResPath(mapData.MapFile);
            Log.Info($"[SaveLoad] Loading map z={mapData.ZLevel} from {resPath}...");

            if (!_mapLoader.TryLoadMap(resPath, out var newMap, out _, opts) || !newMap.HasValue)
            {
                Log.Error($"[SaveLoad] Failed to load map from '{mapData.MapFile}'!");
                continue;
            }

            var mapUid = newMap.Value.Owner;
            oldIdToNewUid[mapData.MapId] = mapUid;
            mapsByZLevel[mapData.ZLevel] = mapUid;
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
        }
    }

    private void PostLoadEntityCleanup()
    {
        var savedQuery = EntityQueryEnumerator<NibiruSavedPlayerComponent>();
        while (savedQuery.MoveNext(out var uid, out _))
        {
            var ssd = EnsureComp<SSDIndicatorComponent>(uid);
            ssd.IsSSD = true;
            ssd.NextUpdate = _gameTiming.CurTime + ssd.UpdateInterval;
            EnsureComp<NibiruNoSSDSleepComponent>(uid);
        }

        var mobQuery = EntityQueryEnumerator<MobStateComponent>();
        while (mobQuery.MoveNext(out var uid, out _))
        {
            ResetPhysiology(uid);
        }
    }

    private void ResetPhysiology(EntityUid uid)
    {
        if (TryComp<RespiratorComponent>(uid, out var respirator))
        {
            _respirator.ResetTimer((uid, respirator));
            _respirator.UpdateSaturation(uid, 5.0f, respirator);
            _respirator.Inhale((uid, respirator));
        }

        if (TryComp<SSDIndicatorComponent>(uid, out var ssd))
        {
            ssd.NextUpdate = _gameTiming.CurTime + ssd.UpdateInterval;
            ssd.IsSSD = true;
            Dirty(uid, ssd);
        }
    }

    #endregion
}

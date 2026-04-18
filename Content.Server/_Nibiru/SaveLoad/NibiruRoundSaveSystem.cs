using System.IO;
using System.Linq;
using System.Text.Json;
using Content.Server._CE.ZLevels.Core;
using Content.Server._CE.ZLevels.Core.Components;
using Content.Server.GameTicking;
using Content.Server.Maps;
using Content.Server.Mind;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._Nibiru.GameTicking.Rules;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Components;
using Robust.Server.GameObjects;
using Robust.Shared.ContentPack;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Map.Components;
using Robust.Shared.Utility;
using Robust.Shared.Network;
using Robust.Shared.Enums;
using Content.Shared._Nibiru.SaveLoad;
using Content.Shared.Mind;
using Content.Shared.Players;
using Robust.Shared.Timing;

namespace Content.Server._Nibiru.SaveLoad;

public sealed class NibiruRoundSaveSystem : EntitySystem
{
    [Dependency] private readonly IResourceManager _res = default!;
    [Dependency] private readonly GameTicker _ticker = default!;
    [Dependency] private readonly IGameMapManager _mapManager = default!;
    [Dependency] private readonly IMapManager _map = default!;
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly CEZLevelsSystem _zLevels = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;

    public string? SaveToLoad { get; private set; }
    private bool _savingLivingEntities = false;

    public override void Initialize()
    {
        base.Initialize();
        _mapLoader.OnIsSerializable += OnIsSerializable;
        SubscribeNetworkEvent<RequestSavedCharacterMessage>(OnSaveCharacterRequest);
    }

    private void OnSaveCharacterRequest(RequestSavedCharacterMessage msg, EntitySessionEventArgs args)
    {
        CheckAndSendSavedCharacterInfo(args.SenderSession);
    }

    private void CheckAndSendSavedCharacterInfo(ICommonSession session)
    {
        var userId = session.UserId.ToString();
        var query = EntityQueryEnumerator<NibiruSavedPlayerComponent, MetaDataComponent>();
        var characterNames = new HashSet<string>();
        while (query.MoveNext(out _, out var saved, out var meta))
        {
            if (saved.UserId == userId && !string.IsNullOrWhiteSpace(meta.EntityName))
            {
                characterNames.Add(meta.EntityName);
            }
        }

        RaiseNetworkEvent(new SavedCharacterAvailableMessage(characterNames.ToList()), session.Channel);
    }

    /// <summary>
    /// Reconnects a player to their saved entity. Used for late-join reconnection.
    /// Uses the same two-step observer → entity approach as SpawnPlayer in NibiruWorldSystem
    /// to ensure the client's game UI is fully initialized before entity attachment.
    /// </summary>
    public void TryLoadSavedPlayer(ICommonSession player, string? targetCharacter = null)
    {
        var userId = player.UserId.ToString();
        var mindSystem = EntityManager.System<MindSystem>();
        var savedQuery = EntityQueryEnumerator<NibiruSavedPlayerComponent, MetaDataComponent>();

        while (savedQuery.MoveNext(out var uid, out var saved, out var meta))
        {
            if (saved.UserId != userId)
                continue;

            if (targetCharacter != null && meta.EntityName != targetCharacter)
                continue;

            Log.Info($"Nibiru: Reconnecting {player.Name} to saved entity {uid} ({meta.EntityName}).");

            // Remove the saved marker immediately to prevent double-reconnect.
            RemComp<NibiruSavedPlayerComponent>(uid);

            var savedEntity = uid;
            var session = player;
            var data = player.ContentData();

            _ticker.PlayerJoinGame(session, true);

            var newMind = mindSystem.CreateMind(data!.UserId, meta.EntityName);
            mindSystem.SetUserId(newMind, data.UserId);


                if (session.Status == SessionStatus.Disconnected)
                {
                    Log.Warning($"Nibiru: {session.Name} disconnected before deferred reconnect to {savedEntity}.");
                    return;
                }

                if (!Exists(savedEntity))
                {
                    Log.Warning($"Nibiru: Saved entity {savedEntity} for {session.Name} no longer exists.");
                    return;
                }

                var mind = session.GetMind();
                if (mind == null)
                {
                    Log.Warning($"Nibiru: No mind found for {session.Name} during deferred reconnect.");
                    return;
                }

                // Ensure the entity has MindContainerComponent before transfer.
                //mindSystem.MakeSentient(savedEntity);

                // Transfer from observer ghost → saved entity. The ghost auto-deletes.
                mindSystem.TransferTo(newMind, savedEntity);

                Log.Info($"Nibiru: Successfully reconnected {session.Name} to saved entity {savedEntity}.");


            return;
        }

        Log.Warning($"Nibiru: No saved entity found for {player.Name} (target: {targetCharacter ?? "any"}).");
    }

    private void OnIsSerializable(Entity<MetaDataComponent> ent, ref bool serializable)
    {
        if (!_savingLivingEntities && HasComp<MobStateComponent>(ent))
        {
            serializable = false;
        }
    }

    public void SaveRound(string savename)
    {
        var basePath = new ResPath($"/Saves/{savename}");

        if (!_res.UserData.Exists(basePath))
        {
            _res.UserData.CreateDir(basePath);
        }

        var preset = _ticker.CurrentPreset?.ID ?? "sandbox";

        var mapFolder = basePath / "Maps";
        _res.UserData.CreateDir(mapFolder);

        var manifest = new RoundSaveManifest()
        {
            PresetId = preset
        };

        // Identify networks
        var networkIds = new Dictionary<EntityUid, int>();
        int nextNetworkId = 0;
        var networkQuery = EntityQueryEnumerator<CEZLevelsNetworkComponent>();
        while (networkQuery.MoveNext(out var uid, out _))
        {
            networkIds[uid] = nextNetworkId++;
        }

        // Save Maps
        foreach (var mapId in _map.GetAllMapIds())
        {
            if (mapId == MapId.Nullspace) continue;

            var mapUid = _map.GetMapEntityId(mapId);
            if (!Exists(mapUid)) continue;

            int zLevel = 0;
            int networkId = -1;

            if (TryComp<CEZLevelMapComponent>(mapUid, out var mapZLevelComp))
            {
                zLevel = mapZLevelComp.Depth;
                if (_zLevels.TryGetZNetwork(mapUid, out var zNetwork))
                {
                    networkId = networkIds.GetValueOrDefault(zNetwork.Value.Owner, -1);
                }
            }
            else
            {
                var rules = EntityQuery<NibiruSurvivalRuleComponent>();
                bool found = false;
                foreach (var rule in rules)
                {
                    if (rule.WorldMap == mapUid) { zLevel = 0; found = true; }
                    else if (rule.CaveMap == mapUid) { zLevel = -1; found = true; }
                }
                if (!found) continue;
            }

            var mapFile = mapFolder / $"map_{(int)mapId}.yml";
            if (_mapLoader.TrySaveMap(mapId, mapFile))
            {
                manifest.Maps.Add(new MapSaveData()
                {
                    MapId = (int)mapId,
                    ZLevel = zLevel,
                    NetworkId = networkId,
                    MapFile = mapFile.ToString()
                });
            }
        }

        // Save Living Entities
        _savingLivingEntities = true;

        // Temporarily make mob prototypes savable
        var impactedProtos = new HashSet<EntityPrototype>();
        var mobProtoQuery = EntityQueryEnumerator<MobStateComponent, MetaDataComponent>();
        while (mobProtoQuery.MoveNext(out _, out _, out var meta))
        {
            if (meta.EntityPrototype != null && !meta.EntityPrototype.MapSavable)
            {
                impactedProtos.Add(meta.EntityPrototype);
                meta.EntityPrototype.MapSavable = true;
            }
        }

        try
        {
            var playerFolder = basePath / "Players";
            _res.UserData.CreateDir(playerFolder);

            var npcFolder = basePath / "Mobs";
            _res.UserData.CreateDir(npcFolder);
            var npcFile = npcFolder / "npcs.yml";

            var npcsToSave = new HashSet<EntityUid>();
            var playersToSave = new List<EntityUid>();

            var query = EntityQueryEnumerator<MetaDataComponent, TransformComponent>();
            while (query.MoveNext(out var uid, out var meta, out var xform))
            {
                if (HasComp<MapComponent>(uid) || HasComp<MapGridComponent>(uid))
                    continue;

                if (!HasComp<MobStateComponent>(uid))
                    continue;

                var mapId = (int)xform.MapID;
                var parentComp = EnsureComp<NibiruSaveParentComponent>(uid);
                parentComp.MapId = mapId;

                if (TryComp<MindContainerComponent>(uid, out var mindComp) && mindComp.HasMind)
                {
                    playersToSave.Add(uid);
                }
                else
                {
                    npcsToSave.Add(uid);
                }
            }

            Log.Info($"Nibiru Save: Found {playersToSave.Count} players and {npcsToSave.Count} NPCs to save.");

            var saveOpts = new SerializationOptions { ErrorOnOrphan = false };

            foreach (var uid in playersToSave)
            {
                var meta = Comp<MetaDataComponent>(uid);
                var userId = string.Empty;
                if (TryComp<ActorComponent>(uid, out var actor))
                {
                    userId = actor.PlayerSession.UserId.ToString();
                }
                else
                {
                    userId = $"entity_{uid}";
                }

                var playerFile = playerFolder / $"{userId}.yml";
                if (_mapLoader.TrySaveEntity(uid, playerFile, saveOpts))
                {
                    manifest.Players.Add(new PlayerSaveData()
                    {
                        UserId = userId,
                        EntityName = meta.EntityName,
                        File = playerFile.ToString()
                    });
                }

                RemComp<NibiruSaveParentComponent>(uid);
            }

            if (npcsToSave.Count > 0)
            {
                if (_mapLoader.TrySaveGeneric(npcsToSave, npcFile, out _, saveOpts))
                {
                    manifest.NpcFile = npcFile.ToString();
                    Log.Info($"Nibiru Save: Saved {npcsToSave.Count} NPCs to {npcFile}");
                }
                else
                {
                    Log.Error($"Nibiru Save: Failed to save {npcsToSave.Count} NPCs!");
                }

                foreach (var npc in npcsToSave)
                {
                    RemComp<NibiruSaveParentComponent>(npc);
                }
            }
        }
        finally
        {
            foreach (var proto in impactedProtos)
            {
                proto.MapSavable = false;
            }
            _savingLivingEntities = false;
        }

        var manifestPath = basePath / "manifest.json";
        var jsonStr = JsonSerializer.Serialize(manifest, new JsonSerializerOptions() { WriteIndented = true });

        using (var stream = _res.UserData.OpenWriteText(manifestPath))
        {
            stream.Write(jsonStr);
        }

        Log.Info($"Nibiru: Round saved to '{savename}'. Preset: {preset}");
    }

    public void RequestLoad(string savename)
    {
        var basePath = new ResPath($"/Saves/{savename}");
        var manifestPath = basePath / "manifest.json";

        if (!_res.UserData.Exists(manifestPath))
        {
            Log.Error($"Nibiru: Save '{savename}' not found!");
            return;
        }

        RoundSaveManifest? manifest;
        using (var stream = _res.UserData.OpenRead(manifestPath))
        {
            manifest = JsonSerializer.Deserialize<RoundSaveManifest>(stream);
        }

        if (manifest != null && !string.IsNullOrEmpty(manifest.PresetId))
        {
            _ticker.SetGamePreset(manifest.PresetId, force: false);
            SaveToLoad = savename;
            _ticker.RestartRound();
            Log.Info($"Nibiru: Loading save '{savename}' with preset '{manifest.PresetId}'.");
        }
    }

    public void ClearLoad()
    {
        SaveToLoad = null;
    }

    public bool LoadSavedMaps(out EntityUid loadedCave, out EntityUid loadedWorld, out EntityUid loadedSky1, out EntityUid loadedSky2)
    {
        loadedCave = EntityUid.Invalid;
        loadedWorld = EntityUid.Invalid;
        loadedSky1 = EntityUid.Invalid;
        loadedSky2 = EntityUid.Invalid;

        if (SaveToLoad == null) return false;

        Log.Info($"Nibiru: Loading saved maps from '{SaveToLoad}'.");

        var basePath = new ResPath($"/Saves/{SaveToLoad}");
        var manifestPath = basePath / "manifest.json";

        if (!_res.UserData.Exists(manifestPath))
        {
            Log.Error($"Nibiru: Manifest not found at {manifestPath}!");
            return false;
        }

        RoundSaveManifest? manifest;
        using (var stream = _res.UserData.OpenRead(manifestPath))
        {
            manifest = JsonSerializer.Deserialize<RoundSaveManifest>(stream);
        }

        if (manifest == null)
        {
            Log.Error("Nibiru: Failed to deserialize manifest!");
            return false;
        }

        Log.Info($"Nibiru: Manifest: {manifest.Maps.Count} maps, {manifest.Players.Count} players, NPC file: {manifest.NpcFile ?? "none"}.");

        // Load maps and reconstruct Z-networks
        var networks = new Dictionary<int, Dictionary<EntityUid, int>>();
        var loadedMapsByZ = new Dictionary<int, EntityUid>();
        var oldToNewMapMapping = new Dictionary<int, EntityUid>();

        foreach (var mapData in manifest.Maps)
        {
            if (_mapLoader.TryLoadMap(new ResPath(mapData.MapFile), out var newMapUid, out _))
            {
                var mapUid = newMapUid.Value.Owner;
                oldToNewMapMapping[mapData.MapId] = mapUid;

                if (mapData.NetworkId != -1)
                {
                    if (!networks.ContainsKey(mapData.NetworkId))
                        networks[mapData.NetworkId] = new();

                    networks[mapData.NetworkId].Add(mapUid, mapData.ZLevel);
                }

                loadedMapsByZ[mapData.ZLevel] = mapUid;
                Log.Info($"Nibiru: Loaded map '{mapData.MapFile}' as UID {mapUid} (Z={mapData.ZLevel}).");
            }
            else
            {
                Log.Error($"Nibiru: Failed to load map '{mapData.MapFile}'!");
            }
        }

        foreach (var networkMaps in networks.Values)
        {
            var newNetwork = _zLevels.CreateZNetwork();
            _zLevels.TryAddMapsIntoZNetwork(newNetwork, networkMaps);
        }

        // Load entity files (players and NPCs)
        var entityFiles = new List<string>();
        foreach (var p in manifest.Players)
            entityFiles.Add(p.File);
        if (!string.IsNullOrEmpty(manifest.NpcFile))
            entityFiles.Add(manifest.NpcFile);

        foreach (var file in entityFiles)
        {
            var resPath = new ResPath(file);
            if (!_res.UserData.Exists(resPath))
            {
                Log.Error($"Nibiru: Entity file not found: {file}");
                continue;
            }

            if (_mapLoader.TryLoadGeneric(resPath, out var result))
            {
                int reparented = 0;
                var isPlayerFile = file.Contains("Players/");
                var userId = isPlayerFile ? Path.GetFileNameWithoutExtension(file) : string.Empty;

                foreach (var uid in result.Entities.Concat(result.Orphans))
                {
                    bool isRoot = HasComp<NibiruSaveParentComponent>(uid);

                    if (isPlayerFile && isRoot)
                    {
                        var savedComp = EnsureComp<NibiruSavedPlayerComponent>(uid);
                        savedComp.UserId = userId;
                    }

                    if (TryComp<NibiruSaveParentComponent>(uid, out var parentComp))
                    {
                        if (oldToNewMapMapping.TryGetValue(parentComp.MapId, out var targetMap))
                        {
                            _xform.SetParent(uid, targetMap);
                            reparented++;
                        }
                        RemComp<NibiruSaveParentComponent>(uid);
                    }
                }

                Log.Info($"Nibiru: Loaded {result.Entities.Count + result.Orphans.Count} entities from '{file}', re-parented {reparented}.");
            }
            else
            {
                Log.Error($"Nibiru: Failed to load entity file: {file}");
            }
        }

        loadedCave = loadedMapsByZ.GetValueOrDefault(-1, EntityUid.Invalid);
        loadedWorld = loadedMapsByZ.GetValueOrDefault(0, EntityUid.Invalid);
        loadedSky1 = loadedMapsByZ.GetValueOrDefault(1, EntityUid.Invalid);
        loadedSky2 = loadedMapsByZ.GetValueOrDefault(2, EntityUid.Invalid);

        Log.Info($"Nibiru: Maps loaded — World: {loadedWorld}, Cave: {loadedCave}.");
        return true;
    }
}

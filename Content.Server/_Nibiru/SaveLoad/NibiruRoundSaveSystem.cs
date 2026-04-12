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
    [Dependency] private readonly Robust.Server.Player.IPlayerManager _playerManager = default!;


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
        while (query.MoveNext(out var uid, out var saved, out var meta))
        {
            if (saved.UserId == userId && !string.IsNullOrWhiteSpace(meta.EntityName))
            {
                characterNames.Add(meta.EntityName);
            }
        }
        
        RaiseNetworkEvent(new SavedCharacterAvailableMessage(characterNames.ToList()), session.Channel);
    }

    public void TryLoadSavedPlayer(ICommonSession player, string? targetCharacter = null)
    {
        var savedQuery = EntityQueryEnumerator<NibiruSavedPlayerComponent, MetaDataComponent>();
        var userId = player.UserId.ToString();
        var mindSystem = EntityManager.System<MindSystem>();
        var playerManager = IoCManager.Resolve<Robust.Server.Player.IPlayerManager>();

        while (savedQuery.MoveNext(out var uid, out var saved, out var meta))
        {
            if (saved.UserId == userId && (targetCharacter == null || meta.EntityName == targetCharacter))
            {
                Log.Info($"Nibiru: Reconnecting player {player.Name} to saved entity {uid}.");
                
                var ticker = EntityManager.System<GameTicker>();
                ticker.PlayerJoinGame(player);

                RemComp<ActorComponent>(uid);
                
                // Nibiru: Clean up any ghost minds to prevent Assert crash in MindSystem.TryGetMind
                if (mindSystem.TryGetMind(player.UserId, out var existingMindId, out var existingMindComp))
                {
                    // If the mind already thinks it owns this entity, we must detach it first 
                    // to avoid 'TransferTo' early return logic (if (entity == mind.OwnedEntity) return;)
                    mindSystem.TransferTo(existingMindId.Value, null, createGhost: false, mind: existingMindComp);
                }

                // Ensure the target entity also thinks it is empty
                RemComp<MindContainerComponent>(uid);
                mindSystem.MakeSentient(uid);

                var xform = Transform(uid);
                if (xform.MapID == MapId.Nullspace || xform.MapUid == null)
                {
                    var spawnPoint = ticker.GetObserverSpawnPoint();
                    var transformSystem = EntityManager.System<SharedTransformSystem>();
                    transformSystem.SetCoordinates(uid, spawnPoint);
                    Log.Info($"Nibiru: Rescued {player.Name} from nullspace and moved to spawn point BEFORE mind transfer.");
                }

                mindSystem.ControlMob(player.UserId, uid);
                
                RemComp<NibiruSavedPlayerComponent>(uid);
                return;
            }
        }
        
        Log.Warning($"Nibiru: Failed to find saved entity for user {player.Name}.");
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
            // ... (existing map saving logic)
            // Note: mobs are excluded via OnIsSerializable
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

        // Temporarily make mob prototypes savable to allow MapLoaderSystem to process them
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

                // Check if it's a living entity (MobState) or something we want to save specifically
                // Users might want other things saved too, but for now we focus on mobs as requested
                if (!HasComp<MobStateComponent>(uid))
                    continue;

                // NPC or Player
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
            // Restore prototypes
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

        Log.Info($"Round saved successfully to {savename}. Preset: {preset}");
    }

    public void RequestLoad(string savename)
    {
        var basePath = new ResPath($"/Saves/{savename}");
        var manifestPath = basePath / "manifest.json";

        if (!_res.UserData.Exists(manifestPath))
        {
            Log.Error($"Save {savename} not found!");
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
            Log.Info($"Loaded round preset {manifest.PresetId} from save {savename}. Priority map override engaged.");
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

        var basePath = new ResPath($"/Saves/{SaveToLoad}");
        var manifestPath = basePath / "manifest.json";

        if (!_res.UserData.Exists(manifestPath)) return false;

        RoundSaveManifest? manifest;
        using (var stream = _res.UserData.OpenRead(manifestPath))
        {
            manifest = JsonSerializer.Deserialize<RoundSaveManifest>(stream);
        }

        if (manifest == null) return false;

        // Group loaded maps by their original network ID
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
            }
        }

        // Reconstruct Z-Networks
        foreach (var networkMaps in networks.Values)
        {
            var newNetwork = _zLevels.CreateZNetwork();
            _zLevels.TryAddMapsIntoZNetwork(newNetwork, networkMaps);
        }

        // Load NPCs and Players
        var entityFiles = new List<string>();
        foreach (var p in manifest.Players) entityFiles.Add(p.File);
        if (!string.IsNullOrEmpty(manifest.NpcFile))
            entityFiles.Add(manifest.NpcFile);

        Log.Info($"Loading {entityFiles.Count} entity files from save...");

        foreach (var file in entityFiles)
        {
            var resPath = new ResPath(file);
            if (!_res.UserData.Exists(resPath))
            {
                Log.Error($"Entity save file not found: {file}");
                continue;
            }

            if (_mapLoader.TryLoadGeneric(resPath, out var result))
            {
                int count = 0;
                var isPlayerFile = file.Contains("Players/");
                var userId = isPlayerFile ? Path.GetFileNameWithoutExtension(file) : string.Empty;

                foreach (var uid in result.Entities.Concat(result.Orphans))
                {
                    if (isPlayerFile)
                    {
                        var savedComp = EnsureComp<NibiruSavedPlayerComponent>(uid);
                        savedComp.UserId = userId;
                    }

                    if (TryComp<NibiruSaveParentComponent>(uid, out var parentComp))
                    {
                        if (oldToNewMapMapping.TryGetValue(parentComp.MapId, out var targetMap))
                        {
                            _xform.SetParent(uid, targetMap);
                            count++;
                        }
                        RemComp<NibiruSaveParentComponent>(uid);
                    }
                }
                Log.Info($"Loaded and re-parented {count} entities from {file}");
            }
            else
            {
                Log.Error($"Failed to load entity file: {file}");
            }
        }

        loadedCave = loadedMapsByZ.GetValueOrDefault(-1, EntityUid.Invalid);
        loadedWorld = loadedMapsByZ.GetValueOrDefault(0, EntityUid.Invalid);
        loadedSky1 = loadedMapsByZ.GetValueOrDefault(1, EntityUid.Invalid);
        loadedSky2 = loadedMapsByZ.GetValueOrDefault(2, EntityUid.Invalid);

        return true;
    }
}

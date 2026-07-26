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
using Content.Shared.SSDIndicator;
using Content.Shared.Atmos;
using System.Reflection;
using Robust.Shared.Analyzers;
using Content.Shared.Body.Components;
using Content.Server.Body.Systems;
using Content.Server.Body.Components;
using Content.Shared._Nibiru.Factions;
using Content.Server._Nibiru.Factions;
using Content.Shared.Nutrition.Components;
using Content.Server.Parallax;

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
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly RespiratorSystem _respirator = default!;
    [Dependency] private readonly BiomeSystem _biome = default!;

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

            // Remove the saved marker immediately to prevent double-reconnect.
            RemComp<NibiruSavedPlayerComponent>(uid);

            var savedEntity = uid;
            var session = player;
            var data = player.ContentData();

            _ticker.PlayerJoinGame(session, true);

            var newMind = mindSystem.CreateMind(data!.UserId, meta.EntityName);
            mindSystem.SetUserId(newMind, data.UserId);

            if (session.Status == SessionStatus.Disconnected)
                return;

            if (!Exists(savedEntity))
                return;

            var mind = session.GetMind();
            if (mind == null)
                return;

            // Transfer from observer ghost → saved entity. The ghost auto-deletes.
            mindSystem.TransferTo(newMind, savedEntity);
            RemComp<Content.Shared.SSDIndicator.SSDIndicatorComponent>(savedEntity);
            return;
        }
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
        Log.Info($"[SaveLoad] Начало сохранения раунда '{savename}'...");
        var basePath = new ResPath($"/Saves/{savename}");

        if (!_res.UserData.Exists(basePath))
        {
            _res.UserData.CreateDir(basePath);
        }

        var preset = _ticker.CurrentPreset?.ID ?? "sandbox";
        Log.Info($"[SaveLoad] Пресет: '{preset}'");

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
        Log.Info($"[SaveLoad] Найдено Z-сетей: {networkIds.Count}");

        // Снимок FactionRegistry: сохраняем Leader/Members и очищаем перед сохранением карты,
        // чтобы NetEntity-ссылки на игроков не попали в карту и не вызывали ошибки при загрузке.
        var factionRegistrySnapshot = new Dictionary<EntityUid, Dictionary<string, (NetEntity leader, List<NetEntity> members)>>();
        var factionRegistryQuery = EntityQueryEnumerator<FactionRegistryComponent>();
        while (factionRegistryQuery.MoveNext(out var regUid, out var registry))
        {
            var snap = new Dictionary<string, (NetEntity, List<NetEntity>)>();
            foreach (var (name, data) in registry.Factions)
            {
                snap[name] = (data.Leader, new List<NetEntity>(data.Members));
                var cleared = data;
                cleared.Leader = NetEntity.Invalid;
                cleared.Members = new List<NetEntity>();
                registry.Factions[name] = cleared;
            }
            factionRegistrySnapshot[regUid] = snap;
        }

        try
        {
            // Save Maps
            var allMapIds = _map.GetAllMapIds().ToList();
            Log.Info($"[SaveLoad] Всего карт в мире: {allMapIds.Count} (включая Nullspace)");

            foreach (var mapId in allMapIds)
            {
                if (mapId == MapId.Nullspace) continue;

                var mapUid = _map.GetMapEntityId(mapId);
                if (!Exists(mapUid))
                {
                    Log.Warning($"[SaveLoad] Карта {mapId}: entity не существует, пропускаем.");
                    continue;
                }

                var mapName = MetaData(mapUid).EntityName;
                int zLevel = 0;
                int networkId = -1;
                bool shouldSave = false;

                if (TryComp<CEZLevelMapComponent>(mapUid, out var mapZLevelComp))
                {
                    // У карты есть CEZLevelMapComponent — используем Depth напрямую
                    zLevel = mapZLevelComp.Depth;
                    shouldSave = true;
                    if (_zLevels.TryGetZNetwork(mapUid, out var zNetwork))
                    {
                        networkId = networkIds.GetValueOrDefault(zNetwork.Value.Owner, -1);
                    }
                    Log.Debug($"[SaveLoad] Карта {mapId} '{mapName}': CEZLevelMapComponent.Depth={zLevel}, networkId={networkId}");
                }
                else
                {
                    // CEZLevelMapComponent — [UnsavedComponent], не сохраняется в yml.
                    // Определяем уровень альтернативными методами.

                    // 1) Попытка через NibiruSurvivalRuleComponent (WorldMap/CaveMap)
                    var rules = EntityQuery<NibiruSurvivalRuleComponent>();
                    foreach (var rule in rules)
                    {
                        if (rule.WorldMap == mapUid)
                        {
                            zLevel = 0; shouldSave = true;
                            Log.Debug($"[SaveLoad] Карта {mapId} '{mapName}': определена как WorldMap (z=0) через Rule.");
                        }
                        else if (rule.CaveMap == mapUid)
                        {
                            zLevel = -1; shouldSave = true;
                            Log.Debug($"[SaveLoad] Карта {mapId} '{mapName}': определена как CaveMap (z=-1) через Rule.");
                        }
                    }

                    // 2) Fallback: по имени entity карты ("level -1", "level 0", "level 1", "level 2")
                    if (!shouldSave && !string.IsNullOrEmpty(mapName))
                    {
                        if (mapName == "level -1") { zLevel = -1; shouldSave = true; }
                        else if (mapName == "level 0") { zLevel = 0; shouldSave = true; }
                        else if (mapName == "level 1") { zLevel = 1; shouldSave = true; }
                        else if (mapName == "level 2") { zLevel = 2; shouldSave = true; }

                        if (shouldSave)
                            Log.Debug($"[SaveLoad] Карта {mapId} '{mapName}': определена по имени entity (z={zLevel}).");
                    }

                    if (!shouldSave)
                    {
                        Log.Warning($"[SaveLoad] Карта {mapId} '{mapName}': не удалось определить Z-уровень, пропускаем.");
                        continue;
                    }
                }

                var mapFile = mapFolder / $"map_{(int)mapId}.yml";
                Log.Info($"[SaveLoad] Сохранение карты {mapId} '{mapName}' (z={zLevel}) -> {mapFile}...");

                // BiomeComponent хранит LoadedEntities (загруженные Entity чанков биома) с EntityUid-ключами.
                // При сериализации удалённые entity превращаются в NetEntity.Invalid (ключ "invalid"),
                // что вызывает ArgumentException: "An item with the same key has already been added".
                // Биом восстановится процедурно по сиду при загрузке, поэтому очищать безопасно.
                var biomeSnap = _biome.PrepareMapForSave(mapUid);
                if (biomeSnap.HasValue)
                    Log.Debug($"[SaveLoad] BiomeComponent на карте {mapId}: очищено LoadedEntities ({biomeSnap.Value.Entities.Count} чанков) и LoadedChunks для сохранения.");

                bool mapSaved;
                try
                {
                    mapSaved = _mapLoader.TrySaveMap(mapId, mapFile);
                }
                finally
                {
                    // Восстанавливаем рантайм-состояние BiomeComponent
                    if (biomeSnap.HasValue)
                        _biome.RestoreMapAfterSave(mapUid, biomeSnap.Value);
                }

                if (mapSaved)
                {
                    manifest.Maps.Add(new MapSaveData()
                    {
                        MapId = (int)mapId,
                        ZLevel = zLevel,
                        NetworkId = networkId,
                        MapFile = mapFile.ToString()
                    });
                    Log.Info($"[SaveLoad] Карта {mapId} сохранена успешно.");
                }
                else
                {
                    Log.Error($"[SaveLoad] Не удалось сохранить карту {mapId} '{mapName}'!");
                }
            }

            Log.Info($"[SaveLoad] Сохранено карт: {manifest.Maps.Count}");
        }
        finally
        {
            // Восстанавливаем Leader/Members в FactionRegistry
            foreach (var (regUid, snap) in factionRegistrySnapshot)
            {
                if (!TryComp<FactionRegistryComponent>(regUid, out var registry))
                    continue;
                foreach (var (name, (leader, members)) in snap)
                {
                    if (!registry.Factions.TryGetValue(name, out var data))
                        continue;
                    data.Leader = leader;
                    data.Members = members;
                    registry.Factions[name] = data;
                }
            }
        }

        // Save Living Entities
        Log.Info("[SaveLoad] Сохранение живых существ...");
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
                parentComp.Position = _xform.GetWorldPosition(xform);
                parentComp.Rotation = _xform.GetWorldRotation(xform);

                if (TryComp<MindContainerComponent>(uid, out var mindComp) && mindComp.HasMind)
                {
                    playersToSave.Add(uid);
                }
                else
                {
                    npcsToSave.Add(uid);
                }
            }

            Log.Info($"[SaveLoad] Игроков для сохранения: {playersToSave.Count}, NPC: {npcsToSave.Count}");

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
                Log.Debug($"[SaveLoad] Сохранение игрока '{meta.EntityName}' ({userId}) -> {playerFile}");

                if (_mapLoader.TrySaveEntity(uid, playerFile, saveOpts))
                {
                    manifest.Players.Add(new PlayerSaveData()
                    {
                        UserId = userId,
                        EntityName = meta.EntityName,
                        File = playerFile.ToString()
                    });
                    Log.Info($"[SaveLoad] Игрок '{meta.EntityName}' сохранён.");
                }
                else
                {
                    Log.Error($"[SaveLoad] Не удалось сохранить игрока '{meta.EntityName}' ({userId})!");
                }

                RemComp<NibiruSaveParentComponent>(uid);
            }

            if (npcsToSave.Count > 0)
            {
                Log.Debug($"[SaveLoad] Сохранение NPC ({npcsToSave.Count} шт.) -> {npcFile}");
                if (_mapLoader.TrySaveGeneric(npcsToSave, npcFile, out _, saveOpts))
                {
                    manifest.NpcFile = npcFile.ToString();
                    Log.Info($"[SaveLoad] NPC сохранены.");
                }
                else
                {
                    Log.Error($"[SaveLoad] Не удалось сохранить NPC!");
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

        Log.Info($"[SaveLoad] Сохранение '{savename}' завершено. Карт: {manifest.Maps.Count}, Игроков: {manifest.Players.Count}.");
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

        Log.Info($"[SaveLoad] Начало загрузки сохранения '{SaveToLoad}'...");

        var basePath = new ResPath($"/Saves/{SaveToLoad}");
        var manifestPath = basePath / "manifest.json";

        if (!_res.UserData.Exists(manifestPath))
        {
            Log.Error($"[SaveLoad] Манифест не найден: {manifestPath}!");
            return false;
        }

        RoundSaveManifest? manifest;
        using (var stream = _res.UserData.OpenRead(manifestPath))
        {
            manifest = JsonSerializer.Deserialize<RoundSaveManifest>(stream);
        }

        if (manifest == null)
        {
            Log.Error("[SaveLoad] Не удалось десериализовать манифест!");
            return false;
        }

        Log.Info($"[SaveLoad] Манифест: {manifest.Maps.Count} карт, {manifest.Players.Count} игроков, NpcFile={manifest.NpcFile ?? "нет"}.");

        // Load maps and reconstruct Z-networks
        var networks = new Dictionary<int, Dictionary<EntityUid, int>>();
        var loadedMapsByZ = new Dictionary<int, EntityUid>();
        var oldToNewMapMapping = new Dictionary<int, EntityUid>();

        foreach (var mapData in manifest.Maps)
        {
            Log.Info($"[SaveLoad] Загрузка карты z={mapData.ZLevel}, file={mapData.MapFile}, oldMapId={mapData.MapId}...");

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
                Log.Info($"[SaveLoad] Карта z={mapData.ZLevel} загружена: newUid={mapUid} (oldId={mapData.MapId})");
            }
            else
            {
                Log.Error($"[SaveLoad] Не удалось загрузить карту '{mapData.MapFile}' (z={mapData.ZLevel})!");
            }
        }

        Log.Info($"[SaveLoad] Старые ID -> новые Entity: [{string.Join(", ", oldToNewMapMapping.Select(kv => $"{kv.Key}->{kv.Value}"))}]");

        foreach (var networkMaps in networks.Values)
        {
            var newNetwork = _zLevels.CreateZNetwork();
            _zLevels.TryAddMapsIntoZNetwork(newNetwork, networkMaps);
            Log.Info($"[SaveLoad] Создана Z-сеть с {networkMaps.Count} картами.");
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
                Log.Error($"[SaveLoad] Файл entity не найден: {file}");
                continue;
            }

            Log.Info($"[SaveLoad] Загрузка entity-файла: {file}");

            if (_mapLoader.TryLoadGeneric(resPath, out var result))
            {
                var isPlayerFile = file.Contains("Players/");
                var userId = isPlayerFile ? Path.GetFileNameWithoutExtension(file) : string.Empty;

                Log.Debug($"[SaveLoad] Загружено entity: {result.Entities.Count} + orphans: {result.Orphans.Count}");

                foreach (var uid in result.Entities.Concat(result.Orphans))
                {
                    bool isRoot = HasComp<NibiruSaveParentComponent>(uid);

                    if (isPlayerFile && isRoot)
                    {
                        Log.Debug($"[SaveLoad] Восстановление игрока uid={uid}, userId={userId}");
                        var savedComp = EnsureComp<NibiruSavedPlayerComponent>(uid);
                        savedComp.UserId = userId;

                        var ssd = EnsureComp<SSDIndicatorComponent>(uid);
                        ssd.IsSSD = true;
                        EnsureComp<NibiruNoSSDSleepComponent>(uid);
                    }

                    // Явный сброс Respirator: насыщение кислородом на максимум и сброс таймера
                    if (TryComp<RespiratorComponent>(uid, out var respirator))
                    {
                        _respirator.UpdateSaturation(uid, respirator.MaxSaturation - respirator.Saturation, respirator);
                        _respirator.ResetTimer((uid, respirator));
                        Log.Debug($"[SaveLoad] Respirator uid={uid}: сброс насыщения до макс ({respirator.MaxSaturation}), сброс таймера.");
                    }

                    // Явный сброс лёгких: восстановить газовую смесь в лёгких до нормального уровня
                    if (TryComp<LungComponent>(uid, out var lung))
                    {
                        // Заполняем лёгкие нормальным воздухом: ~21% O2, 79% N2
                        lung.Air.SetMoles(Gas.Oxygen, 1.5f);
                        lung.Air.SetMoles(Gas.Nitrogen, 5.6f);
                        //lung.Air.Temperature = Atmospherics.NormalBodyTemperature;
                        Log.Debug($"[SaveLoad] Lung uid={uid}: восстановлена газовая смесь.");
                    }

                    // Явный сброс SSDIndicator таймеров
                    if (TryComp<SSDIndicatorComponent>(uid, out var ssdComp))
                    {
                        ssdComp.NextUpdate = _gameTiming.CurTime + ssdComp.UpdateInterval;
                        //ssdComp.FallAsleepTime = TimeSpan.Zero;
                    }

                    // Явный сброс таймеров голода и жажды
                    if (TryComp<HungerComponent>(uid, out var hunger))
                    {
                        var curTime = _gameTiming.CurTime;
                        // Сбрасываем время последнего авторитарного обновления голода
                        // (без [AutoPausedField] это поле не сбрасывается автоматически)
                        //hunger.LastAuthoritativeHungerChangeTime = curTime;
                        //hunger.NextThresholdUpdateTime = curTime + hunger.ThresholdUpdateRate;
                    }
                    if (TryComp<ThirstComponent>(uid, out var thirst))
                    {
                        //thirst.NextUpdateTime = _gameTiming.CurTime + thirst.UpdateRate;
                    }

                    // Дополнительный сброс всех [AutoPausedField] на случай других компонентов
                    ResetAllAutoPausedFields(uid);

                    if (TryComp<NibiruSaveParentComponent>(uid, out var parentComp))
                    {
                        if (oldToNewMapMapping.TryGetValue(parentComp.MapId, out var targetMap))
                        {
                            Log.Debug($"[SaveLoad] Размещение uid={uid}: mapId={parentComp.MapId} -> targetMap={targetMap}, pos={parentComp.Position}");
                            _xform.SetParent(uid, targetMap);
                            _xform.SetLocalPositionRotation(uid, parentComp.Position, parentComp.Rotation);

                            if (TryComp<MapComponent>(targetMap, out var mapComp)
                                && _map.TryFindGridAt(mapComp.MapId, parentComp.Position, out var gridUid, out _))
                            {
                                _xform.SetParent(uid, gridUid);
                            }
                        }
                        else
                        {
                            Log.Error($"[SaveLoad] uid={uid}: нет маппинга для сохранённого mapId={parentComp.MapId}! Доступные: [{string.Join(", ", oldToNewMapMapping.Keys)}]");
                        }
                        RemComp<NibiruSaveParentComponent>(uid);
                    }
                }
            }
            else
            {
                Log.Error($"[SaveLoad] Не удалось загрузить entity-файл: {file}");
            }
        }

        loadedCave = loadedMapsByZ.GetValueOrDefault(-1, EntityUid.Invalid);
        loadedWorld = loadedMapsByZ.GetValueOrDefault(0, EntityUid.Invalid);
        loadedSky1 = loadedMapsByZ.GetValueOrDefault(1, EntityUid.Invalid);
        loadedSky2 = loadedMapsByZ.GetValueOrDefault(2, EntityUid.Invalid);

        return true;
    }

    /// <summary>
    /// Dynamically finds all [AutoPausedField] components on an entity
    /// and resets their TimeSpan timers to current server time.
    /// This fixes the "rapid breathing / metabolism" glitch after save reloads.
    /// </summary>
    private void ResetAllAutoPausedFields(EntityUid uid)
    {
        var curTime = _gameTiming.CurTime;

        foreach (var component in EntityManager.GetComponents(uid))
        {
            var compType = component.GetType();

            // Check fields
            foreach (var field in compType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (Attribute.IsDefined(field, typeof(AutoPausedFieldAttribute)))
                {
                    if (field.FieldType == typeof(TimeSpan))
                    {
                        var val = (TimeSpan)field.GetValue(component)!;
                        // If it's zero or in the past, reset it to now
                        if (val == TimeSpan.Zero || val < curTime)
                            field.SetValue(component, curTime);
                    }
                    else if (field.FieldType == typeof(TimeSpan?))
                    {
                        var val = (TimeSpan?)field.GetValue(component);
                        if (val != null && (val.Value == TimeSpan.Zero || val.Value < curTime))
                            field.SetValue(component, curTime);
                    }
                }
            }

            // Check properties
            foreach (var prop in compType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (Attribute.IsDefined(prop, typeof(AutoPausedFieldAttribute)))
                {
                    if (prop.PropertyType == typeof(TimeSpan))
                    {
                        var val = (TimeSpan)prop.GetValue(component)!;
                        if (val == TimeSpan.Zero || val < curTime)
                            prop.SetValue(component, curTime);
                    }
                    else if (prop.PropertyType == typeof(TimeSpan?))
                    {
                        var val = (TimeSpan?)prop.GetValue(component);
                        if (val != null && (val.Value == TimeSpan.Zero || val.Value < curTime))
                            prop.SetValue(component, curTime);
                    }
                }
            }
        }
    }
}

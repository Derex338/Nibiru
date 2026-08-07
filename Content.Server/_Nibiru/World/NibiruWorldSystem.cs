using Content.Server._CE.ZLevels.Core;
using Content.Server._Nibiru.SaveLoad;
using Content.Server.Administration.Managers;
using Content.Server.Atmos.EntitySystems;
using Content.Server.GameTicking;
using Content.Server.Mind;
using Content.Server.Parallax;
using Content.Server.Station.Systems;
using Content.Shared._CE.DayCycle;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._CE.ZLevels.Light.Components;
using Content.Shared._CE.ZLevels.Roof;
using Content.Shared._Nibiru.GameTicking.Rules;
using Content.Shared._Nibiru.World;
using Content.Shared.Administration;
using Content.Shared.Atmos;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Content.Shared.Gravity;
using Content.Shared.Light.Components;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using System.Linq;
using System.Numerics;

namespace Content.Server._Nibiru.World;

/// Взято за основу с RimFortress
public sealed partial class NibiruWorldSystem : SharedNibiruWorldSystem
{
[Dependency] private MindSystem _mind = default!;
[Dependency] private BiomeSystem _biome = default!;
[Dependency] private MapSystem _map = default!;
[Dependency] private IPrototypeManager _prototype = default!;
[Dependency] private StationSpawningSystem _stationSpawn = default!;
[Dependency] private CEZLevelsSystem _ceZLevels = default!;
[Dependency] private AtmosphereSystem _atmos = default!;
[Dependency] private IRobustRandom _random = default!;
[Dependency] private MetaDataSystem _metadata = default!;
[Dependency] private IConfigurationManager _cfg = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LoadingMapsEvent>(OnLoadingMaps);
    }

    private void OnLoadingMaps(LoadingMapsEvent ev)
    {
        var saveSys = EntityManager.System<Content.Server._Nibiru.SaveLoad.NibiruRoundSaveSystem>();
        if (saveSys.SaveToLoad != null)
        {
            // Clear maps selected in lobby to let our save have priority
            ev.Maps.Clear();
        }
    }

    public EntityUid InitializeWorld(NibiruSurvivalRuleComponent rule)
    {
        Rule = rule;

        var saveSys = EntityManager.System<Content.Server._Nibiru.SaveLoad.NibiruRoundSaveSystem>();
        if (saveSys.SaveToLoad != null)
        {
            var success = saveSys.LoadSavedMaps(out var loadedCave, out var loadedWorld, out _);
            if (success)
            {
                rule.WorldMap = loadedWorld;
                rule.CaveMap = loadedCave;
                saveSys.ClearLoad();
                return loadedWorld;
            }
        }

        // Создаем сеть Z-уровней
        var network = _ceZLevels.CreateZNetwork();

        // Генерируем общий сид для синхронизации высот
        var seed = new Random().Next();

        var skyLevelsCount = _cfg.GetCVar(CCVars.ZLevelsCount);

        // 1. Создаем подземный мир (шахта) - Уровень -1
        var caveMap = _map.CreateMap();
        _metadata.SetEntityName(caveMap, "level -1");
        _biome.EnsurePlanet(caveMap, _prototype.Index(rule.CaveBiome), seed);
        EnsureComp<CEZLevelMapRoofComponent>(caveMap);
        EnsureComp<SunLightRayComponent>(caveMap);

        // 2. Создаем основной мир (планета) - Уровень 0
        var worldMap = _map.CreateMap();
        _metadata.SetEntityName(worldMap, "level 0");
        _biome.EnsurePlanet(worldMap, _prototype.Index(rule.Biome), seed);
        EnsureComp<CEDayCycleComponent>(worldMap);
        EnsureComp<CEZLevelMapRoofComponent>(worldMap);
        EnsureComp<SunLightRayComponent>(worldMap);

        // Создаем skyLevelsCount уровней неба (уровни 1..N)
        var skyMaps = new List<EntityUid>();
        var zNetworkMaps = new Dictionary<EntityUid, int>
        {
            { caveMap, -1 },
            { worldMap, 0 }
        };

        for (var i = 1; i <= skyLevelsCount; i++)
        {
            var skyMap = _map.CreateMap();
            _metadata.SetEntityName(skyMap, $"level {i}");
            SetupUpperLayer(skyMap);
            skyMaps.Add(skyMap);
            zNetworkMaps[skyMap] = i;
        }

        // Добавляем все карты в сеть
        _ceZLevels.TryAddMapsIntoZNetwork(network, zNetworkMaps);

        if (HasComp<LightCycleComponent>(caveMap))
            RemComp<LightCycleComponent>(caveMap);
        if (HasComp<MapLightComponent>(caveMap))
            RemComp<MapLightComponent>(caveMap);

        // Настройка цикла дня и ночи
        foreach (var map in new[] { worldMap }.Concat(skyMaps))
        {
            if (TryComp(map, out LightCycleComponent? cycle))
            {
                cycle.Duration = rule.DayDuration;
                cycle.Offset = rule.DayDuration / 3; // For roundstart day time
                cycle.InitialOffset = false;
                cycle.MinLightLevel = 1f;
            }
        }

        rule.WorldMap = worldMap;
        rule.CaveMap = caveMap;

        return worldMap;
    }

    private void SetupUpperLayer(EntityUid mapUid)
    {
        EnsureComp<MapGridComponent>(mapUid);

        var gravity = EnsureComp<GravityComponent>(mapUid);
        gravity.Enabled = true;
        gravity.Inherent = true;

        var light = EnsureComp<MapLightComponent>(mapUid);
        light.AmbientLightColor = Color.FromHex("#D8B059");

        EnsureComp<RoofComponent>(mapUid);
        EnsureComp<LightCycleComponent>(mapUid);
        EnsureComp<SunShadowComponent>(mapUid);
        EnsureComp<SunShadowCycleComponent>(mapUid);
        EnsureComp<CEDayCycleComponent>(mapUid);
        EnsureComp<CEZLevelMapRoofComponent>(mapUid);
        EnsureComp<SunLightRayComponent>(mapUid);

        var moles = new float[Atmospherics.AdjustedNumberOfGases];
        moles[(int)Gas.Oxygen] = 21.824779f;
        moles[(int)Gas.Nitrogen] = 82.10312f;

        var mixture = new GasMixture(moles, Atmospherics.T20C);
        _atmos.SetMapAtmosphere(mapUid, false, mixture);
    }

    /// <summary>
    /// Creates or allocates a free map for the player.
    /// Called from PlayerBeforeSpawnEvent. If a saved entity exists, reconnects to it.
    /// Otherwise spawns a new character.
    /// </summary>
    public EntityUid? SpawnPlayer(PlayerBeforeSpawnEvent ev)
    {
        if (Rule is not { } rule)
            return null;

        if (rule.WorldMap == EntityUid.Invalid)
            return null;

        // Check for a saved body matching this player
        var userId = ev.Player.UserId;
        var savedQuery = EntityQueryEnumerator<NibiruSavedPlayerComponent, MetaDataComponent>();

        while (savedQuery.MoveNext(out var uid, out var saved, out var meta))
        {
            return uid;
            /*if (saved.UserId != userId.ToString())
                continue;

            // In Nibiru, we force use of the saved character if one exists for the user.
            Log.Info($"Nibiru: Found saved entity {uid} ({meta.EntityName}) for {ev.Player.Name}. Forcing reconnect.");

            // Remove the saved marker immediately to prevent any double-reconnect attempts.
            RemComp<NibiruSavedPlayerComponent>(uid);

            var savedEntity = uid;
            var session = ev.Player;

            if (session.Status == SessionStatus.Disconnected)
                return null;

            if (!Exists(savedEntity))
                return null;

            // Get player's active session mind or create one if absent
            if (!_mind.TryGetMind(session, out var mindId, out _))
            {
                mindId = _mind.CreateMind(userId, meta.EntityName);
            }

            // Transfer mind & session from ghost/lobby -> saved entity. Ghost auto-deletes.
            _mind.TransferTo(mindId, savedEntity);
            RemComp<Content.Shared._Nibiru.SaveLoad.NibiruNoSSDSleepComponent>(savedEntity);
            if (TryComp<Content.Shared.SSDIndicator.SSDIndicatorComponent>(savedEntity, out var ssd))
            {
                ssd.IsSSD = false;
                Dirty(savedEntity, ssd);
            }

            return savedEntity;*/
        }

        // No saved entity — spawn a new character normally.
        var spawnTiles = GetSpawnTiles(1);
        EntityCoordinates coords;

        if (spawnTiles.Count > 0)
        {
            coords = Turf.GetTileCenter(spawnTiles.First());
        }
        else
        {
            Log.Warning($"Nibiru: Could not find spawn tiles on map {rule.WorldMap}! Falling back to (0,0).");
            coords = new EntityCoordinates(rule.WorldMap, Vector2.Zero);
        }

        var spawnBox = Box2.CenteredAround(coords.Position, new Vector2(SpawnAreaRadius));
        var freeTiles = GetFreeTiles(rule.WorldMap, spawnBox, MinSpawnAreaTiles);

        if (freeTiles.Count > 0)
        {
            coords = Turf.GetTileCenter(_random.Pick(freeTiles));
        }

        var newMind = _mind.CreateMind(ev.Player.UserId, ev.Player.Name);
        _mind.SetUserId(newMind, ev.Player.UserId);

        var mob = _stationSpawn.SpawnPlayerMob(coords, null, ev.Profile, null, null);
        _mind.TransferTo(newMind, mob);

        if (HasComp<CEActiveZPhysicsComponent>(mob))
        {
            RemComp<CEActiveZPhysicsComponent>(mob);
            Timer.Spawn(1000, () => EnsureComp<CEActiveZPhysicsComponent>(mob));
        }

        return mob;
    }
}

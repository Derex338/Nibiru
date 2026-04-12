using System.Linq;
using System.Numerics;
using Content.Server.Administration.Managers;
using Content.Server.Mind;
using Content.Server.Parallax;
using Content.Server.Preferences.Managers;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Server._CE.ZLevels.Core;
using Content.Shared._Nibiru.GameTicking.Rules;
using Content.Shared._Nibiru.World;
using Content.Server._Nibiru.SaveLoad;
//using Content.Shared._Nibiru.CCVar;
using Content.Shared.Administration;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.GameTicking;
using Content.Server.GameTicking;
using Content.Shared.GameTicking.Components;
using Content.Shared.Light.Components;
using Content.Shared.Parallax.Biomes;
using Content.Shared.Pinpointer;
using Content.Shared.Preferences;
using Content.Shared.Station.Components;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Gravity;
using Content.Shared._CE.DayCycle;
using Robust.Shared.Maths;
using Content.Shared._CE.ZLevels.Roof;
using Robust.Shared.Random;

namespace Content.Server._Nibiru.World;

/// Взято за основу с RimFortress
public sealed class NibiruWorldSystem : SharedNibiruWorldSystem
{
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly BiomeSystem _biome = default!;
    [Dependency] private readonly MapSystem _map = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly StationSpawningSystem _stationSpawn = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly CEZLevelsSystem _ceZLevels = default!;
    [Dependency] private readonly AtmosphereSystem _atmos = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

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
        /*var stations = _station.GetStations();
        foreach (var station in stations)
        {
            QueueDel(station);
        }*/

        Rule = rule;

        var saveSys = EntityManager.System<Content.Server._Nibiru.SaveLoad.NibiruRoundSaveSystem>();
        if (saveSys.SaveToLoad != null)
        {
            var success = saveSys.LoadSavedMaps(out var loadedCave, out var loadedWorld, out var loadedSky1, out var loadedSky2);
            if (success)
            {
                rule.WorldMap = loadedWorld;
                rule.CaveMap = loadedCave;
                saveSys.ClearLoad(); // сбрасываем статус загрузки после успешного применения
                return loadedWorld;
            }
        }



        // Создаем сеть Z-уровней
        var network = _ceZLevels.CreateZNetwork();

        // Генерируем общий сид для синхронизации высот
        var seed = new Random().Next();

        // 1. Создаем подземный мир (шахта) - Уровень -1
        var caveMap = _map.CreateMap();
        _biome.EnsurePlanet(caveMap, _prototype.Index(rule.CaveBiome), seed);
        EnsureComp<CEZLevelMapRoofComponent>(caveMap);


        // 2. Создаем основной мир (планета) - Уровень 0
        var worldMap = _map.CreateMap();
        _biome.EnsurePlanet(worldMap, _prototype.Index(rule.Biome), seed);
        EnsureComp<CEDayCycleComponent>(worldMap);
        EnsureComp<CEZLevelMapRoofComponent>(worldMap);

        // Уровень 1
        var sky1Map = _map.CreateMap();
        SetupUpperLayer(sky1Map);

        // Уровень 2
        var sky2Map = _map.CreateMap();
        SetupUpperLayer(sky2Map);

        // Добавляем все карты в сеть
        _ceZLevels.TryAddMapsIntoZNetwork(network, new Dictionary<EntityUid, int>
        {
            { caveMap, -1 },
            { worldMap, 0 },
            { sky1Map, 1 },
            { sky2Map, 2 }
        });

        // Настройка компонентов карты для основного мира
        /*if (TryComp<MapComponent>(worldMap, out var mapComp))
        {
            EnsureComp<StationDataComponent>(worldMap);
            foreach (var grid in _mapManager.GetAllGrids(mapComp.MapId))
                _station.AddGridToStation(worldMap, grid.Owner);
            EnsureComp<StationEventEligibleComponent>(worldMap);
        }*/

        if (HasComp<LightCycleComponent>(caveMap))
            RemComp<LightCycleComponent>(caveMap);
        if (HasComp<MapLightComponent>(caveMap))
            RemComp<MapLightComponent>(caveMap);

        // Настройка цикла дня и ночи
        foreach (var map in new[] { worldMap, sky1Map, sky2Map })
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

        var moles = new float[Atmospherics.AdjustedNumberOfGases];
        moles[(int)Gas.Oxygen] = 21.824779f;
        moles[(int)Gas.Nitrogen] = 82.10312f;

        var mixture = new GasMixture(moles, Atmospherics.T20C);
        _atmos.SetMapAtmosphere(mapUid, false, mixture);
    }

    /// <summary>
    /// Creates or allocates a free map for the player
    /// </summary>
    public EntityUid? SpawnPlayer(PlayerBeforeSpawnEvent ev)
    {
        if (Rule is not { } rule)
            return null;

        if (rule.WorldMap == EntityUid.Invalid)
            return null;

        // Check for saved body first
        var userId = ev.Player.UserId.ToString();
        var selectedName = ev.Profile.Name;
        var savedQuery = EntityQueryEnumerator<NibiruSavedPlayerComponent, MetaDataComponent>();
        while (savedQuery.MoveNext(out var uid, out var saved, out var meta))
        {
            if (saved.UserId == userId)
            {
                if (meta.EntityName == selectedName)
                {
                    Log.Info($"Nibiru: Auto-reconnecting player {ev.Player.Name} to saved entity {uid} at round start because profile matches.");

                    RemComp<ActorComponent>(uid);
                    
                    // Nibiru: Clean up any ghost minds to prevent Assert crash in MindSystem.TryGetMind
                    if (_mind.TryGetMind(ev.Player.UserId, out var existingMindId, out var existingMindComp))
                    {
                        // If the mind already thinks it owns this entity, we must detach it first 
                        // to avoid 'TransferTo' early return logic (if (entity == mind.OwnedEntity) return;)
                        _mind.TransferTo(existingMindId.Value, null, createGhost: false, mind: existingMindComp);
                    }

                    // Ensure the target entity also thinks it is empty
                    RemComp<MindContainerComponent>(uid);
                    _mind.MakeSentient(uid);

                    var xform = Transform(uid);
                    if (xform.MapID == MapId.Nullspace || xform.MapUid == null)
                    {
                        var spawnPoint = _gameTicker.GetObserverSpawnPoint();
                        _transform.SetCoordinates(uid, spawnPoint);
                        Log.Info($"Nibiru: Rescued {ev.Player.Name} from nullspace and moved to spawn point BEFORE mind transfer.");
                    }

                    _mind.ControlMob(ev.Player.UserId, uid);
                    
                    RemComp<NibiruSavedPlayerComponent>(uid);
                    return uid;
                }
                else
                {
                    Log.Info($"Nibiru: Not auto-reconnecting player {ev.Player.Name} because profile '{selectedName}' doesn't match saved entity '{meta.EntityName}'.");
                }
            }
        }

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

        // Spawn player entity
        var newMind = _mind.CreateMind(ev.Player.UserId, ev.Player.Name);
        _mind.SetUserId(newMind, ev.Player.UserId);

        var mob = _stationSpawn.SpawnPlayerMob(coords, null, ev.Profile, null, null);
        _mind.TransferTo(newMind, mob);

        return mob;
    }
}

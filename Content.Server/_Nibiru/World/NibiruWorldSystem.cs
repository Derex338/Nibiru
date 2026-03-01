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
//using Content.Shared._Nibiru.CCVar;
using Content.Shared.Administration;
using Content.Shared.GameTicking;
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

    public override void Initialize()
    {
        base.Initialize();
    }

    public EntityUid InitializeWorld(NibiruSurvivalRuleComponent rule)
    {
        /*var stations = _station.GetStations();
        foreach (var station in stations)
        {
            QueueDel(station);
        }*/

        Rule = rule;

        // Создаем сеть Z-уровней
        var network = _ceZLevels.CreateZNetwork();

        // Генерируем общий сид для синхронизации высот
        var seed = new Random().Next();

        // 1. Создаем подземный мир (шахта) - Уровень -1
        var caveMap = _map.CreateMap();
        _biome.EnsurePlanet(caveMap, _prototype.Index(rule.CaveBiome), seed);


        // 2. Создаем основной мир (планета) - Уровень 0
        var worldMap = _map.CreateMap();
        _biome.EnsurePlanet(worldMap, _prototype.Index(rule.Biome), seed);
        EnsureComp<CEDayCycleComponent>(worldMap);

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

        var coords = Turf.GetTileCenter(GetSpawnTiles(1).First());
        var spawnBox = Box2.CenteredAround(coords.Position, new Vector2(SpawnAreaRadius));
        var freeTiles = GetFreeTiles(rule.WorldMap, spawnBox, MinSpawnAreaTiles);

        if (freeTiles.Count == 0)
            return null;

        // Spawn player entity
        var newMind = _mind.CreateMind(ev.Player.UserId, ev.Player.Name);
        _mind.SetUserId(newMind, ev.Player.UserId);

        var mob = _stationSpawn.SpawnPlayerMob(coords, null, ev.Profile, null, null);
        _mind.TransferTo(newMind, mob);

        return mob;
    }
}

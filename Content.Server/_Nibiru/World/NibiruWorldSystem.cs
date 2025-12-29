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

namespace Content.Server._Nibiru.World;

/// <summary>
/// Взято за основу с RimFortress
/// </summary>
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

    public override void Initialize()
    {
        base.Initialize();
    }

    public EntityUid InitializeWorld(NibiruSurvivalRuleComponent rule)
    {
        var stations = _station.GetStations();
        foreach (var station in stations)
        {
            QueueDel(station);
        }

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


        // 3. Создаем горные слои - Уровни 1 и 2

        // Уровень 1
        var sky1Map = _map.CreateMap();
        if (rule.MountainL1Biome != null    )
            _biome.EnsurePlanet(sky1Map, _prototype.Index(rule.MountainL1Biome), seed);

        // Уровень 2
        var sky2Map = _map.CreateMap();
        if (rule.MountainL2Biome != null)
            _biome.EnsurePlanet(sky2Map, _prototype.Index(rule.MountainL2Biome), seed);

        // Добавляем все карты в сеть
        _ceZLevels.TryAddMapsIntoZNetwork(network, new Dictionary<EntityUid, int>
        {
            { caveMap, -1 },
            { worldMap, 0 },
            { sky1Map, 1 },
            { sky2Map, 2 }
        });

        // Настройка компонентов карты для основного мира
        if (TryComp<MapComponent>(worldMap, out var mapComp))
        {
            EnsureComp<StationDataComponent>(worldMap);
            foreach (var grid in _mapManager.GetAllGrids(mapComp.MapId))
                _station.AddGridToStation(worldMap, grid.Owner);
            EnsureComp<StationEventEligibleComponent>(worldMap);
        }

        // Настройка цикла дня и ночи для основного мира
        if (TryComp(worldMap, out LightCycleComponent? cycle))
        {
            cycle.Duration = rule.DayDuration;
            cycle.Offset = rule.DayDuration / 3; // For roundstart day time
            cycle.InitialOffset = false;
            cycle.MinLightLevel = 1f;
        }

        rule.WorldMap = worldMap;
        rule.CaveMap = caveMap;

        return worldMap;
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

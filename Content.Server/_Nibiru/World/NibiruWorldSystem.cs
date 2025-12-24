using System.Linq;
using System.Numerics;
using Content.Server.Administration.Managers;
using Content.Server.Mind;
using Content.Server.Parallax;
using Content.Server.Preferences.Managers;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared._Nibiru.GameTicking.Rules;
using Content.Shared._Nibiru.World;
//using Content.Shared._Nibiru.CCVar;
using Content.Shared.Administration;
using Content.Shared.GameTicking;
using Content.Shared.Light.Components;
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

    public override void Initialize()
    {
        base.Initialize();


    }

    public EntityUid InitializeWorld(NibiruSurvivalRuleComponent rule)
    {
        Rule = rule;
        var map = _map.CreateMap();
        _biome.EnsurePlanet(map, _prototype.Index(rule.Biome));

        if (TryComp<MapComponent>(map, out var mapComp))
        {
            EnsureComp<StationDataComponent>(map);
            foreach (var grid in _mapManager.GetAllGrids(mapComp.MapId))
                _station.AddGridToStation(map, grid.Owner);
            EnsureComp<StationEventEligibleComponent>(map);
        }

        var cave = _map.CreateMap();
        _biome.EnsurePlanet(cave, _prototype.Index(rule.CaveBiome));

        if (TryComp(map, out LightCycleComponent? cycle))
        {
            cycle.Duration = rule.DayDuration;
            cycle.Offset = rule.DayDuration / 3; // For roundstart day time
            cycle.InitialOffset = false;
            cycle.MinLightLevel = 1f;
        }

        rule.WorldMap = map;
        rule.CaveMap = cave;
        return map;
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

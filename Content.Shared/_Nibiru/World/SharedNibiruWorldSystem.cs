using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared._Nibiru.GameTicking.Rules;
using Content.Shared.Maps;
using Content.Shared.Parallax.Biomes;
using Content.Shared.Pinpointer;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Serialization;

namespace Content.Shared._Nibiru.World;

// From RimFortress

/// <summary>
/// Manages world and generation
/// </summary>
public abstract partial class SharedNibiruWorldSystem : EntitySystem
{
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] protected TurfSystem Turf = default!;
    [Dependency] private SharedBiomeSystem _biome = default!;

    protected NibiruSurvivalRuleComponent? Rule;

    private int _playerSafeRadius = 100;
    protected int SpawnAreaRadius = 20;
    protected int MinSpawnAreaTiles = 100;

    public override void Initialize()
    {
        base.Initialize();
    }
}

[Serializable, NetSerializable]
public sealed class SettlementCoordinatesMessage(Dictionary<NetEntity, List<NetCoordinates>> coords) : EntityEventArgs
{
    public Dictionary<NetEntity, List<NetCoordinates>> Coords = coords;
}

[Serializable, NetSerializable]
public sealed class WorldDebugInfoRequest : EntityEventArgs
{
}

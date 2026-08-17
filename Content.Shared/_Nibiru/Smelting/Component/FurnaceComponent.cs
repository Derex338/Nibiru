using Content.Shared.Tag;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Nibiru.Smelting;

/// <summary>
/// Component for a furnace for smelting ores and heating items
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SmeltingFurnaceComponent : Component
{
    [DataField]
    public string ContainerId = "ore_container";

    [DataField]
    public string SolutionContainerId = "solution_container";

    [DataField]
    public string Solution = "smelted_metal";

    /// <summary>
    /// Container for ores and items
    /// </summary>
    [ViewVariables]
    public Container? OreContainer = default!;

    [ViewVariables]
    public ContainerSlot? SolutionContainer = default!;

    /// <summary>
    /// Maximum number of items in the furnace
    /// </summary>
    [DataField]
    public int MaxOreCapacity = 10;

    /// <summary>
    /// White list of tags for items that can be placed in the furnace
    /// </summary>
    [DataField]
    public List<ProtoId<TagPrototype>>? Tags;

    /// <summary>
    /// Temperature at which items start to burn
    /// </summary>
    [DataField]
    public float BurnTemperature = 500f;

    /// <summary>
    /// Sound of melting
    /// </summary>
    [DataField]
    public SoundSpecifier? SmeltingSound;

    /// <summary>
    /// Sound of fully melted ore
    /// </summary>
    [DataField]
    public SoundSpecifier? MeltCompleteSound;

    /// <summary>
    /// Sound of burning
    /// </summary>
    [DataField]
    public SoundSpecifier? BurnSound;
}

[Serializable, NetSerializable]
public enum SmeltingFurnaceVisuals : byte
{
    ContainsOre,
    IsSmelting,
    Temperature
}

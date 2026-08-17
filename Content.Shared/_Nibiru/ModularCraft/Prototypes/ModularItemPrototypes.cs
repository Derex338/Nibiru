using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Nibiru.ModularCraft.Prototypes;

/// <summary>
/// Type of slot in the modular system (blade, handle, shaft, top, etc.)
/// </summary>
[Prototype]
public sealed partial class ModularPartPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; set; } = default!;

    [DataField("name")]
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// Type of modular item to assemble (sword, axe, pickaxe, staff)
/// Determines which slots are needed for assembly.
/// </summary>
[Prototype]
public sealed partial class ModularItemPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; set; } = default!;

    [DataField("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Required slots for this item (e.g., Blade, Guard, Handle)
    /// </summary>
    [DataField("requiredParts")]
    public List<ProtoId<ModularPartPrototype>> RequiredParts { get; set; } = new();

    /// <summary>
    /// ID of the base entity, which will be spawned during crafting
    /// </summary>
    [DataField("baseEntity")]
    public string BaseEntity { get; set; } = default!;
}

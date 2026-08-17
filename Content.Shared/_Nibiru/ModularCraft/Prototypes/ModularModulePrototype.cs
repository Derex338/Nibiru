using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared._Nibiru.ModularCraft.Prototypes;

/// <summary>
/// Module prototype - a specific part of a modular item.
/// </summary>
[Prototype]
public sealed partial class ModularModulePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; set; } = default!;

    [DataField("name")]
    public string Name { get; set; } = string.Empty;

    [DataField("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Type of slot the module belongs to (blade, handle, etc.)
    /// </summary>
    [DataField("partType")]
    public ProtoId<ModularPartPrototype> PartType { get; set; } = default!;

    /// <summary>
    /// Compatible types of weapons/items (optional). If empty - suitable for all.
    /// </summary>
    [DataField("compatibleItemTypes")]
    public List<ProtoId<ModularItemPrototype>> CompatibleItemTypes { get; set; } = new();

    /// <summary>How many units of material are spent on creation</summary>
    [DataField("materialCost")]
    public int MaterialCost { get; set; } = 1;

    [DataField("sprite")]
    public SpriteSpecifier? Sprite { get; set; }

    [DataField("damageBonus")]
    public FixedPoint2 DamageBonus { get; set; } = FixedPoint2.Zero;

    [DataField("reachBonus")]
    public FixedPoint2 ReachBonus { get; set; } = FixedPoint2.Zero;

    [DataField("attackSpeedMultiplier")]
    public FixedPoint2 AttackSpeedMultiplier { get; set; } = FixedPoint2.New(1.0f);

    [DataField("penetrationBonus")]
    public FixedPoint2 PenetrationBonus { get; set; } = FixedPoint2.Zero;

    [DataField("blockBonus")]
    public FixedPoint2 BlockBonus { get; set; } = FixedPoint2.Zero;

    [DataField("weight")]
    public FixedPoint2 Weight { get; set; } = FixedPoint2.New(0.5f);
}

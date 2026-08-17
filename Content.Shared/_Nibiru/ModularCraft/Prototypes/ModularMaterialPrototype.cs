using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared._Nibiru.ModularCraft.Prototypes;

[Prototype]
public sealed partial class ModularMaterialPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; set; } = default!;

    [DataField("name")]
    public string Name { get; set; } = string.Empty;

    [DataField("description")]
    public string Description { get; set; } = string.Empty;

    [DataField("category")]
    public string Category { get; set; } = "Metal";

    [DataField("quality")]
    public int Quality { get; set; } = 1;

    [DataField("texture")]
    public SpriteSpecifier? Texture { get; set; }

    [DataField("previewColor")]
    public string PreviewColor { get; set; } = "#A0A0B0";

    [DataField("damageMultiplier")]
    public FixedPoint2 DamageMultiplier { get; set; } = FixedPoint2.New(1.0f);

    [DataField("durabilityMultiplier")]
    public FixedPoint2 DurabilityMultiplier { get; set; } = FixedPoint2.New(1.0f);

    [DataField("weightMultiplier")]
    public FixedPoint2 WeightMultiplier { get; set; } = FixedPoint2.New(1.0f);

    [DataField("penetrationMultiplier")]
    public FixedPoint2 PenetrationMultiplier { get; set; } = FixedPoint2.New(1.0f);

    [DataField("stackPrototype")]
    public string? StackPrototype { get; set; }
}

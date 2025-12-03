using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Research.Prototypes;

[Prototype("researchEpoch")]
public sealed partial class ResearchEpochPrototype : IPrototype  //Nibiru
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField("name", required: true)]
    public LocId Name = string.Empty;

    [DataField("description")]
    public LocId Description = string.Empty;

    [DataField("order", required: true)]
    public int Order;

    [DataField("color")]
    public Color Color = Color.White;

    [DataField("icon")]
    public SpriteSpecifier? Icon;

    [DataField("unlockNextEpochTech")]
    public ProtoId<TechnologyPrototype>? UnlockNextEpochTech;
}

/// <summary>
/// Событие, когда разблокирована новая эпоха
/// </summary>
[ByRefEvent]
public readonly record struct ResearchEpochUnlockedEvent(ProtoId<ResearchEpochPrototype> EpochId);

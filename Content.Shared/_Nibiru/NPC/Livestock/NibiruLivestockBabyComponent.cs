using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared._Nibiru.NPC.Livestock;

/// <summary>
/// Livestock component for managing juveniles.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class NibiruLivestockBabyComponent : Component
{
    /// <summary>
    /// Sprite specifiers for different growth stages.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public List<SpriteSpecifier> Stages = new();

    /// <summary>
    /// Growth time per stage.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float StageGrowthTime = 300f;

    /// <summary>
    /// Current growth progress.
    /// </summary>
    [ViewVariables]
    public float GrowthAccumulator;

    /// <summary>
    /// Current growth stage index.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public int GrowthStage;

    /// <summary>
    /// Is ready to grow up.
    /// </summary>
    [ViewVariables]
    public bool ReadyToGrow => GrowthAccumulator >= StageGrowthTime;
}

[Serializable, NetSerializable]
public enum BabyStageVisuals : byte
{
    NewBorn,
    Young,
    Adolescent
}

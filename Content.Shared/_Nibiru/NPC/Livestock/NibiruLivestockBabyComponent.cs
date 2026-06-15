using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared._Nibiru.NPC.Livestock;

/// <summary>
/// Компонент животноводства: регулирует детенышей.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class NibiruLivestockBabyComponent : Component
{
    /// <summary>
    /// Массив спрайтов для разных стадий роста.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public List<SpriteSpecifier> Stages = new();

    /// <summary>
    /// Время взросления.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float StageGrowthTime = 300f;

    /// <summary>
    /// Текущий прогресс взросления.
    /// </summary>
    [ViewVariables]
    public float GrowthAccumulator;

    /// <summary>
    /// Текущая стадия взросления.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public int GrowthStage;

    /// <summary>
    /// Готово ли к взрослению.
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

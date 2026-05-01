using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._Nibiru.NPC.Utility;

/// <summary>
/// Компонент для животных, которых можно привязывать верёвкой и вести за собой.
/// Обычное перетаскивание затруднено из-за веса.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NibiruLeashableComponent : Component
{
    #region Sounds

    /// <summary>
    /// Звук привязывания верёвкой.
    /// </summary>
    [DataField]
    public SoundSpecifier? LeashSound;

    #endregion
    /// <summary>
    /// Привязано ли животное верёвкой.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public bool IsLeashed;

    /// <summary>
    /// Кто держит конец верёвки.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public EntityUid? LeashedTo;

    /// <summary>
    /// Прототип верёвки, которой привязано животное.
    /// </summary>
    [ViewVariables]
    public string? RopePrototype;

    /// <summary>
    /// Максимальная длина верёвки (в тайлах).
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float LeashLength = 3f;

    /// <summary>
    /// Множитель сложности обычного перетаскивания.
    /// Чем выше, тем медленнее тащить обычным способом.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float DragDifficultyMultiplier = 3f;

    /// <summary>
    /// Пытается ли животное вырваться из привязи.
    /// Зависит от уровня доверия, если есть NibiruTamableComponent.
    /// </summary>
    [ViewVariables]
    public bool TryingToBreakFree;

    /// <summary>
    /// Шанс вырваться за одну проверку (от 0 до 1).
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float BreakFreeChance = 0.05f;

    /// <summary>
    /// Интервал проверки попытки вырваться (в секундах).
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float BreakFreeInterval = 5f;

    /// <summary>
    /// Таймер попытки вырваться.
    /// </summary>
    [ViewVariables]
    public float BreakFreeAccumulator;
}

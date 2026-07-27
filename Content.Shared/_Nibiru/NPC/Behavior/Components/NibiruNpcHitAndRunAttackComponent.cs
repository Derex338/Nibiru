using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using System.Numerics;

namespace Content.Shared._Nibiru.NPC.Behavior.Components;

/// <summary>
/// Attack phases "bite and leap back" for wolves.
/// </summary>
[Serializable, NetSerializable]
public enum LeapPhase : byte
{
    /// <summary>
    /// Ожидание / преследование цели.
    /// </summary>
    Idle,

    /// <summary>
    /// Выполняет укус (атака в ближнем бою).
    /// </summary>
    Biting,

    /// <summary>
    /// Физический прыжок назад относительно направления тела.
    /// </summary>
    Leaping,

    /// <summary>
    /// Кулдаун после прыжка перед следующим циклом.
    /// </summary>
    Cooldown,
}

/// <summary>
/// Hit-and-run attack component — for wolves and similar predators.
/// The animal approaches the target, bites, then sharply leaps backward
/// relative to its body rotation, waits, and repeats.
/// The leap is implemented via physics impulse, not standard navigation.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NibiruNpcHitAndRunAttackComponent : Component
{
    // ── Runtime-состояние ────────────────────────────────────────────────

    /// <summary>
    /// Текущая фаза цикла атаки.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public LeapPhase Phase = LeapPhase.Idle;

    /// <summary>
    /// Вектор прыжка назад (нормализованный, заполняется в момент укуса).
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public Vector2 LeapDirection;

    /// <summary>
    /// Внутренний таймер текущей фазы.
    /// </summary>
    [ViewVariables]
    public float Timer;

    // ── Настройки (DataField) ─────────────────────────────────────────────

    /// <summary>
    /// Скорость прыжка назад (у.е./с).
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public float LeapSpeed = 9f;

    /// <summary>
    /// Расстояние прыжка (у.е.). Определяет продолжительность фазы Leaping.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public float LeapDistance = 3f;

    /// <summary>
    /// Пауза в секундах после приземления перед следующим циклом.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public float WaitDuration = 1.0f;

    /// <summary>
    /// Дистанция до цели при которой начинается укус (переход в Biting).
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public float AttackRange = 1.5f;
}

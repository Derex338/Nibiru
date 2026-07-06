using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using System.Numerics;

namespace Content.Shared._Nibiru.NPC.Behavior.Components;

/// <summary>
/// Фазы атаки "укусил и отпрыгнул" для волков.
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
/// Компонент атаки "укусил и отпрыгнул" — для волков и схожих хищников.
/// Животное подходит к цели, кусает, затем резко отпрыгивает назад
/// относительно поворота своего тела, выжидает и повторяет.
/// Прыжок реализован через физический импульс, а не стандартную навигацию.
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

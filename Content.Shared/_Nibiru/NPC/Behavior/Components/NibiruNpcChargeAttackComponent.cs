using Content.Shared.Damage;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using System.Numerics;

namespace Content.Shared._Nibiru.NPC.Behavior.Components;

/// <summary>
/// Фазы атаки с разбега для рогатых животных.
/// </summary>
[Serializable, NetSerializable]
public enum ChargePhase : byte
{
    /// <summary>
    /// Ожидание / преследование цели. Переход в WindUp происходит
    /// когда цель попадает в диапазон разбега.
    /// </summary>
    Idle,

    /// <summary>
    /// Животное остановилось, повернулось к цели и трясётся.
    /// Длится <see cref="NibiruNpcChargeAttackComponent.ShakeDuration"/> секунд.
    /// </summary>
    WindUp,

    /// <summary>
    /// Активный разбег — летит по прямой линии через физический импульс.
    /// Тряска снята. Урон всему на пути через коллизии.
    /// Останавливается при истечении <see cref="NibiruNpcChargeAttackComponent.MaxDuration"/>
    /// или при столкновении со статичным объектом.
    /// </summary>
    Charging,

    /// <summary>
    /// Кулдаун после разбега перед возвратом в преследование.
    /// </summary>
    Cooldown,
}

/// <summary>
/// Компонент атаки с разбега — для рогатых животных (козы, коровы, и т.п.).
/// Животное входит в диапазон разбега, останавливается, поворачивается к цели,
/// начинает трястись (WindUp), затем бежит по прямой линии нанося урон
/// всем сущностям на пути. Разбег останавливается при столкновении с твёрдым
/// объектом или по истечении максимального времени.
/// Движение реализовано через физический импульс, без навигации NPC.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NibiruNpcChargeAttackComponent : Component
{
    // ── Runtime-состояние ─────────────────────────────────────────────────

    /// <summary>
    /// Текущая фаза разбега.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public ChargePhase Phase = ChargePhase.Idle;

    /// <summary>
    /// Нормализованный вектор направления разбега.
    /// Фиксируется в момент входа в WindUp и не меняется до конца разбега.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public Vector2 Direction;

    /// <summary>
    /// Внутренний таймер текущей фазы.
    /// </summary>
    [ViewVariables]
    public float Timer;

    /// <summary>
    /// Множество сущностей, уже получивших урон в текущем разбеге.
    /// Сбрасывается при входе в WindUp.
    /// </summary>
    [ViewVariables]
    public HashSet<EntityUid> HitEntities = new();

    // ── Настройки (DataField) ─────────────────────────────────────────────

    /// <summary>
    /// Продолжительность тряски перед разбегом (секунды).
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public float ShakeDuration = 1.0f;

    /// <summary>
    /// Скорость разбега (у.е./с).
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public float Speed = 15f;

    /// <summary>
    /// Максимальная продолжительность разбега (секунды).
    /// Если за это время не было столкновения со стеной — останавливается сам.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public float MaxDuration = 1.5f;

    /// <summary>
    /// Продолжительность кулдауна после разбега (секунды).
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public float CooldownDuration = 5.0f;

    /// <summary>
    /// Минимальная дистанция до цели для начала WindUp.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public float MinChargeDistance = 3f;

    /// <summary>
    /// Максимальная дистанция до цели для начала WindUp.
    /// Если цель дальше — продолжает преследование.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public float MaxChargeDistance = 9f;

    /// <summary>
    /// Урон наносимый каждой сущности при столкновении во время разбега.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public DamageSpecifier? Damage;

    /// <summary>
    /// Сила отбрасывания сущностей при столкновении во время разбега.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public float KnockbackForce = 6f;

    /// <summary>
    /// Останавливать ли разбег при столкновении со статичным объектом (стеной).
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public bool StopOnWallCollision = true;
}

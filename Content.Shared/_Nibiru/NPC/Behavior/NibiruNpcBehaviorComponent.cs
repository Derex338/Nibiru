using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._Nibiru.NPC.Behavior;

/// <summary>
/// Управляет базовым поведением NPC: определяет тип реакции на угрозы,
/// текущее состояние и параметры агрессии/бегства.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NibiruNpcBehaviorComponent : Component
{
    #region Sounds

    /// <summary>
    /// Звук при обнаружении врага (рычание, шипение).
    /// </summary>
    [DataField]
    public SoundSpecifier? AggroSound;

    /// <summary>
    /// Звук атаки.
    /// </summary>
    [DataField]
    public SoundSpecifier? AttackSound;

    /// <summary>
    /// Звук боли при получении урона.
    /// </summary>
    [DataField]
    public SoundSpecifier? HurtSound;

    /// <summary>
    /// Звук смерти.
    /// </summary>
    [DataField]
    public SoundSpecifier? DeathSound;

    /// <summary>
    /// Фоновые звуки (мычание, блеяние, кудахтанье).
    /// </summary>
    [DataField]
    public SoundSpecifier? AmbientSound;

    /// <summary>
    /// Интервал фоновых звуков в секундах.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float AmbientSoundInterval = 15f;

    /// <summary>
    /// Таймер фоновых звуков.
    /// </summary>
    [ViewVariables]
    public float AmbientSoundAccumulator;

    /// <summary>
    /// Звук испуга / бегства.
    /// </summary>
    [DataField]
    public SoundSpecifier? FleeSound;

    #endregion
    /// <summary>
    /// Базовый тип поведения: агрессивный, нейтральный или мирный.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public NibiruNpcBehaviorType BehaviorType = NibiruNpcBehaviorType.Neutral;

    /// <summary>
    /// Стиль ведения боя.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public NibiruCombatStyle CombatStyle = NibiruCombatStyle.Default;

    /// <summary>
    /// Текущее состояние конечного автомата поведения.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public NibiruNpcState CurrentState = NibiruNpcState.Idle;

    /// <summary>
    /// Текущая цель (для атаки или бегства от неё).
    /// </summary>
    [ViewVariables]
    public EntityUid? CurrentTarget;

    /// <summary>
    /// Дальность, на которой агрессивный NPC начнёт преследование.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float AggroRange = 8f;

    /// <summary>
    /// Дальность, на которой NPC прекратит преследование и вернётся.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float DeaggroRange = 15f;

    /// <summary>
    /// Дистанция, на которой мирный NPC начинает убегать от угрозы.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float FleeRange = 6f;

    /// <summary>
    /// Как далеко NPC убегает от источника угрозы.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float FleeDistance = 12f;

    /// <summary>
    /// Время в секундах, которое NPC помнит атакующего после потери из виду.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float MemoryDuration = 30f;

    /// <summary>
    /// Запомненные враги: EntityUid -> время, когда "забудет".
    /// </summary>
    [ViewVariables]
    public Dictionary<EntityUid, TimeSpan> HostileMemory = new();

    /// <summary>
    /// Радиус патрулирования вокруг домашней точки.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float PatrolRadius = 8f;

    /// <summary>
    /// Домашняя позиция, вокруг которой NPC патрулирует.
    /// Устанавливается при спавне.
    /// </summary>
    [ViewVariables]
    public EntityCoordinates? HomePosition;

    /// <summary>
    /// Интервал между сменой точки патруля.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float PatrolInterval = 5f;

    /// <summary>
    /// Таймер до следующей смены точки патруля.
    /// </summary>
    [ViewVariables]
    public float PatrolAccumulator;

    /// <summary>
    /// Таймер для тактических маневров (отскок, разбег).
    /// </summary>
    [ViewVariables]
    public float CombatTimer;

    /// <summary>
    /// Заряжен ли разбег / активен ли он сейчас.
    /// </summary>
    [ViewVariables]
    public bool IsCombatActionActive;
}

/// <summary>
/// Состояния конечного автомата поведения NPC.
/// </summary>
[Serializable, NetSerializable]
public enum NibiruNpcState : byte
{
    /// <summary>
    /// Стоит или бродит без дела.
    /// </summary>
    Idle,

    /// <summary>
    /// Патрулирует территорию вокруг домашней точки.
    /// </summary>
    Patrolling,

    /// <summary>
    /// Преследует цель.
    /// </summary>
    Chasing,

    /// <summary>
    /// Атакует цель в ближнем бою.
    /// </summary>
    Attacking,

    /// <summary>
    /// Убегает от угрозы.
    /// </summary>
    Fleeing,

    /// <summary>
    /// Следует за хозяином (прирученное животное).
    /// </summary>
    Following,

    /// <summary>
    /// Возвращается к домашней точке.
    /// </summary>
    Returning
}

/// <summary>
/// Стили боя для животных.
/// </summary>
[Serializable, NetSerializable]
public enum NibiruCombatStyle : byte
{
    /// <summary>
    /// Обычная атака (просто стоять и бить).
    /// </summary>
    Default,

    /// <summary>
    /// Укусил и отпрыгнул (для волков и собак).
    /// </summary>
    HitAndRun,

    /// <summary>
    /// Атака с разбега (для оленей и коров).
    /// </summary>
    Charge
}

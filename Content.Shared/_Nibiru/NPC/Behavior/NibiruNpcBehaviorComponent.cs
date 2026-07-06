using Content.Shared.Audio;
using Content.Shared.Damage;
using System.Numerics;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._Nibiru.NPC.Behavior;

/// <summary>
/// Управляет базовым поведением NPC: определяет тип реакции на угрозы,
/// текущее состояние и параметры агрессии/бегства.
/// </summary>
[RegisterComponent]
public sealed partial class NibiruNpcBehaviorComponent : Component
{
    // Placeholder
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
    Returning,

    /// <summary>
    /// Голоден и ищет еду.
    /// </summary>
    Hungry,

    /// <summary>
    /// Выполняет атаку с разбега (зарядка или само движение).
    /// </summary>
    Charging
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

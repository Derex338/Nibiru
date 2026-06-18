using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using System.Numerics;

namespace Content.Shared._Nibiru.NPC.Commands;

/// <summary>
/// Компонент для животного, которое вцепилось в цель.
/// Инвертирует обычную механику Pull - цель тащит животное.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NibiruAnimalGrabbedComponent : Component
{
    /// <summary>
    /// Цель, в которую вцепилось животное.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Target;

    /// <summary>
    /// Множитель замедления для цели.
    /// </summary>
    [DataField]
    public float TargetSlowdownMultiplier = 0.6f;

    /// <summary>
    /// Урон каждую секунду пока животное удерживает цель.
    /// </summary>
    [DataField]
    public DamageSpecifier? TickDamage;

    /// <summary>
    /// Накопитель времени для тик-урона.
    /// </summary>
    [ViewVariables]
    public float DamageAccumulator;

    /// <summary>
    /// Интервал тик-урона в секундах.
    /// </summary>
    [DataField]
    public float DamageInterval = 1f;

    /// <summary>
    /// Накопитель времени для встряхивания.
    /// </summary>
    [ViewVariables]
    public float ShakeAccumulator;

    /// <summary>
    /// Интервал смены направления встряхивания (секунды).
    /// </summary>
    [DataField]
    public float ShakeInterval = 0.25f;

    /// <summary>
    /// Амплитуда встряхивания цели (в тайлах).
    /// </summary>
    [DataField]
    public float ShakeAmplitude = 0.4f;

    /// <summary>
    /// Текущее направление встряхивания (+1 или -1).
    /// </summary>
    [ViewVariables]
    public float ShakeDirection = 1f;

    /// <summary>
    /// Длительность DoAfter для отцепления (секунды).
    /// </summary>
    [DataField]
    public float DetachDuration = 3f;
}

[Serializable, NetSerializable]
public sealed partial class NibiruAnimalDetachDoAfterEvent : SimpleDoAfterEvent
{
}

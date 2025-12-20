using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Movement.Components;

/// <summary>
/// Компонент для сущностей, которыми можно управлять через Strap (лошади, транспорт)
/// Работает автоматически при пристёгивании к StrapComponent
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RideableComponent : Component
{
    /// <summary>
    /// Тип передвижения транспорта
    /// </summary>
    [DataField, AutoNetworkedField]
    public RideableLocomotionType LocomotionType = RideableLocomotionType.Legs;

    /// <summary>
    /// Может ли всадник управлять, если транспорт мёртв
    /// </summary>
    [DataField]
    public bool CanMoveWhenDead = false;

    /// <summary>
    /// Состояние спрайта когда на транспорте есть всадник
    /// </summary>
    [DataField]
    public string? MountedState;

    /// <summary>
    /// Базовое состояние спрайта транспорта
    /// </summary>
    [DataField]
    public string? BaseState;

    [DataField]
    public bool NeedSeddle = true;
}

/// <summary>
/// Тип передвижения транспорта
/// </summary>
[Serializable, NetSerializable]
public enum RideableLocomotionType : byte
{
    /// <summary>
    /// Передвижение на ногах (лошади, животные)
    /// </summary>
    Legs,

    /// <summary>
    /// Передвижение на колёсах (велосипеды, машины)
    /// </summary>
    Wheels,

    /// <summary>
    /// Передвижение на гусеницах (танки)
    /// </summary>
    Tracks,

    /// <summary>
    /// Полёт (летающие маунты)
    /// </summary>
    Flight
}

[Serializable, NetSerializable]
public enum RideableVisuals : byte
{
    Mounted,
    Dead
}

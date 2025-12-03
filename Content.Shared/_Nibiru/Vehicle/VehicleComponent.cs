using Content.Shared.Whitelist;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Nibiru.Vehicle;

/// <summary>
/// Компонент для сущностей, на которых можно ездить верхом (лошади, транспорт и т.д.)
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class VehicleComponent : Component
{
    /// <summary>
    /// Тип передвижения транспорта
    /// </summary>
    [DataField, AutoNetworkedField]
    public VehicleLocomotionType LocomotionType = VehicleLocomotionType.Legs;

    /// <summary>
    /// Слот для всадника
    /// </summary>
    [ViewVariables]
    public ContainerSlot RiderSlot = default!;

    [ViewVariables]
    public readonly string RiderSlotId = "mount-rider-slot";

    /// <summary>
    /// Белый список существ, которые могут ездить на этом транспорте
    /// </summary>
    [DataField]
    public EntityWhitelist? RiderWhitelist;

    /// <summary>
    /// Задержка посадки на транспорт
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float MountDelay = 1.5f;

    /// <summary>
    /// Задержка спешивания другого игрока
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float DismountDelay = 2f;

    /// <summary>
    /// Может ли всадник двигаться, если транспорт мёртв
    /// </summary>
    [DataField]
    public bool CanMoveWhenDead = false;

    /// <summary>
    /// Состояние спрайта для визуализации всадника
    /// </summary>
    [DataField]
    public string? RiderState;

    /// <summary>
    /// Базовое состояние спрайта транспорта
    /// </summary>
    [DataField]
    public string? BaseState;

    /// <summary>
    /// Состояние спрайта когда на транспорте есть всадник
    /// </summary>
    [DataField]
    public string? MountedState;

    /// <summary>
    /// Действие для спешивания
    /// </summary>
    [DataField]
    public EntProtoId DismountAction = "ActionDismount";

    [DataField]
    public EntityUid? DismountActionEntity;
}

/// <summary>
/// Тип передвижения транспорта
/// </summary>
[Serializable, NetSerializable]
public enum VehicleLocomotionType : byte
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
public enum MountVisuals : byte
{
    Mounted,
    Dead
}

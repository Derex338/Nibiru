using Robust.Shared.GameStates;

namespace Content.Shared.Movement.Components;

/// <summary>
/// Компонент для сущностей, которые в данный момент управляют транспортом
/// Автоматически добавляется при пристёгивании к RideableComponent
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RiderComponent : Component
{
    /// <summary>
    /// Транспорт, которым управляет всадник
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public EntityUid Rideable;
}

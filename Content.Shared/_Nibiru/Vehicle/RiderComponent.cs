using Robust.Shared.GameStates;

namespace Content.Shared.Movement.Components;

/// <summary>
/// Component for entities currently controlling a vehicle
/// Automatically added when attaching to a RideableComponent
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RiderComponent : Component
{
    /// <summary>
    /// Transport controlled by the rider
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public EntityUid Rideable;
}

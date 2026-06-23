using Robust.Shared.GameStates;

namespace Content.Shared._Nibiru.LobbedFire;

/// <summary>
/// Pending lobbed shot. System waits FlightDuration then spawns the projectile at TargetPosition.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class LobbedProjectileComponent : Component
{
    [DataField, AutoNetworkedField]
    public float FlightDuration = 1f;

    [DataField, AutoNetworkedField]
    public float TimeAlive;
}

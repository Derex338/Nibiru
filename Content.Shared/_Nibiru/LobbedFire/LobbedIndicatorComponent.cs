using System.Numerics;
using Robust.Shared.GameStates;

namespace Content.Shared._Nibiru.LobbedFire;

/// <summary>
/// Added to the indicator entity that shows where a lobbed projectile lands.
/// Grows from small to full size over FlightDuration seconds.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class LobbedIndicatorComponent : Component
{
    [DataField, AutoNetworkedField]
    public float FlightDuration = 1.5f;

    [DataField, AutoNetworkedField]
    public float TimeAlive;
}

using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Nibiru.NPC.Commands;

/// <summary>
/// Temporary component on an animal waiting for the owner to bring an item for sniffing.
/// Added by the Search command and removed when:
///  — the player uses an item on the animal (InteractUsing), or
///  — the timeout expires.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class NibiruAnimalSearchWaitingComponent : Component
{
    /// <summary>
    /// The owner waiting for the sniffing result.
    /// </summary>
    [DataField]
    public EntityUid Commander;

    /// <summary>
    /// How many seconds the animal waits for an item (timeout).
    /// </summary>
    [DataField]
    public float Timeout = 8f;

    /// <summary>
    /// Time accumulator.
    /// </summary>
    [ViewVariables]
    public float Accumulator;
}

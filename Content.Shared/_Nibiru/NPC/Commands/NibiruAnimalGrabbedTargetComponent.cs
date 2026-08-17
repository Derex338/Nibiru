using Robust.Shared.GameStates;

namespace Content.Shared._Nibiru.NPC.Commands;

/// <summary>
/// Component for the target being grabbed by an animal.
/// Applies slowdown to the target.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NibiruAnimalGrabbedTargetComponent : Component
{
    /// <summary>
    /// The animal that grabbed this target.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Grabber;

    /// <summary>
    /// Slowdown multiplier for the target.
    /// </summary>
    [DataField]
    public float SlowdownMultiplier = 0.6f;
}

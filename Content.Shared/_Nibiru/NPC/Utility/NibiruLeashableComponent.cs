using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._Nibiru.NPC.Utility;

/// <summary>
/// Component for animals that can be leashed and led.
/// Regular dragging is difficult due to weight.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NibiruLeashableComponent : Component
{
    #region Sounds

    /// <summary>
    /// Sound of leashing.
    /// </summary>
    [DataField]
    public SoundSpecifier? LeashSound;

    #endregion
    /// <summary>
    /// Whether the animal is leashed.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public bool IsLeashed;

    /// <summary>
    /// Who is holding the end of the rope.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public EntityUid? LeashedTo;

    /// <summary>
    /// Prototype of the rope used to leash the animal.
    /// </summary>
    [ViewVariables]
    public string? RopePrototype;

    /// <summary>
    /// Maximum leash length (in tiles).
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float LeashLength = 3f;

    /// <summary>
    /// Dragging difficulty multiplier.
    /// Higher values mean slower dragging.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float DragDifficultyMultiplier = 3f;

    /// <summary>
    /// Is the animal trying to break free.
    /// Depends on trust level, if NibiruTamableComponent is present.
    /// </summary>
    [ViewVariables]
    public bool TryingToBreakFree;

    /// <summary>
    /// Break free chance per check (0 to 1).
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float BreakFreeChance = 0.05f;

    /// <summary>
    /// Break free check interval (seconds).
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float BreakFreeInterval = 5f;

    /// <summary>
    /// Break free timer.
    /// </summary>
    [ViewVariables]
    public float BreakFreeAccumulator;
}

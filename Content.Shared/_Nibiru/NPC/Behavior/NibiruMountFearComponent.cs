using Robust.Shared.GameStates;
using Robust.Shared.Audio;

namespace Content.Shared._Nibiru.NPC.Behavior;

/// <summary>
/// Component of fear for riding animals.
/// When fear accumulates to the maximum, the animal throws off the rider and runs away.
/// Fear accumulates from damage, number of aggressive entities and nearby players.
/// Stress resistance can be trained.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NibiruMountFearComponent : Component
{
    /// <summary>
    /// Current fear level (0..MaxFear).
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public float FearLevel;

    /// <summary>
    /// Maximum fear level. Reaching it triggers panic.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float MaxFear = 100f;

    /// <summary>
    /// How much fear is added for each point of damage.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float FearPerDamage = 5f;

    /// <summary>
    /// How much fear is added for each aggressive NPC/player in radius.
    /// Checked periodically.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float FearPerNearbyThreat = 2f;

    /// <summary>
    /// The radius around for threat checking (to count aggressors).
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float ThreatScanRadius = 8f;

    /// <summary>
    /// The rate of fear decrease per second (when there are no threats).
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float FearDecayRate = 3f;

    /// <summary>
    /// The interval of threat checking in seconds.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float ThreatCheckInterval = 1f;

    /// <summary>
    /// The timer for checking threats.
    /// </summary>
    [ViewVariables]
    public float ThreatCheckAccumulator;

    /// <summary>
    /// Stress resistance training level (0..MaxTraining).
    /// The higher it is, the less fear accumulates.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public float StressTraining;

    /// <summary>
    /// Maximum stress resistance training level.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float MaxStressTraining = 100f;

    /// <summary>
    /// How much stress resistance experience is gained for each stress tick.
    /// The animal gets used to stress with regular exposure.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float TrainingPerStressTick = 0.1f;

    /// <summary>
    /// Fear reduction multiplier from training (0..1).
    /// Calculated as 1 - (StressTraining / MaxStressTraining * MaxReduction).
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float MaxFearReduction = 0.7f;

    /// <summary>
    /// Fear from nearby fire/torches.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float FearFromFire = 8f;

    /// <summary>
    /// Is the animal in a state of panic.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public bool IsPanicking;

    /// <summary>
    /// Duration of panic after the rider is thrown off (seconds).
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float PanicDuration = 10f;

    /// <summary>
    /// Remaining panic time.
    /// </summary>
    [ViewVariables]
    public float PanicTimer;

    /// <summary>
    /// Sound, when the animal panics.
    /// </summary>
    [DataField]
    public SoundSpecifier? PanicSound;

    /// <summary>
    /// Sound, when the animal is nervous (fear above 50%).
    /// </summary>
    [DataField]
    public SoundSpecifier? NervousSound;
}

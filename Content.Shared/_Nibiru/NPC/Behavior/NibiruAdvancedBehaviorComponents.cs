using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Nibiru.NPC.Behavior;

/// <summary>
/// Territory component: NPC is bound to its lair/nest.
/// Becomes much more aggressive when strangers approach offspring.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NibiruTerritorialComponent : Component
{
    /// <summary>
    /// Territory radius around the lair.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float TerritoryRadius = 10f;

    /// <summary>
    /// Aggression multiplier when a stranger is inside the territory.
    /// Increases AggroRange and reduces the attack threshold.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float TerritoryAggressionMultiplier = 2f;

    /// <summary>
    /// Does the NPC have offspring in the territory.
    /// If yes — aggression is even higher.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public bool HasOffspringNearby;

    /// <summary>
    /// Aggression multiplier when offspring is present.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float OffspringProtectionMultiplier = 3f;

    /// <summary>
    /// Territorial warning sound (roar, growl).
    /// </summary>
    [DataField]
    public SoundSpecifier? WarningSound;
}

/// <summary>
/// Sleep-wake cycle component.
/// The NPC can fall asleep at night or at a certain time, reducing vigilance.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NibiruSleepCycleComponent : Component
{
    /// <summary>
    /// Is the NPC a nocturnal predator (active at night, sleeps during the day).
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public bool IsNocturnal;

    /// <summary>
    /// Is the NPC sleeping right now.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public bool IsSleeping;

    /// <summary>
    /// Perception multiplier during sleep (0.1 = 10% of normal vision/hearing).
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float SleepPerceptionMultiplier = 0.15f;

    /// <summary>
    /// Sleep duration in seconds.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float SleepDuration = 300f;

    /// <summary>
    /// Wake duration in seconds.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float WakeDuration = 600f;

    /// <summary>
    /// Current cycle timer.
    /// </summary>
    [ViewVariables]
    public float CycleAccumulator;

    /// <summary>
    /// Sound of falling asleep.
    /// </summary>
    [DataField]
    public SoundSpecifier? SleepSound;

    /// <summary>
    /// Periodic snoring sound during sleep.
    /// </summary>
    [DataField]
    public SoundSpecifier? SleepingSound;

    /// <summary>
    /// Sound of waking up.
    /// </summary>
    [DataField]
    public SoundSpecifier? WakeSound;
}

/// <summary>
/// Component for fear of fire and light.
/// Wild animals avoid sources of fire.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class NibiruFireFearComponent : Component
{
    /// <summary>
    /// The radius at which the NPC detects fire and begins to avoid it.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float FireDetectionRange = 6f;

    /// <summary>
    /// Flee distance multiplier from fire.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float FireFleeMultiplier = 1.5f;

    /// <summary>
    /// Tags considered as sources of fire.
    /// </summary>
    [DataField, ViewVariables]
    public List<string> FireTags = new() { "Torch", "Campfire", "Bonfire", "Lit" };

    /// <summary>
    /// Sound of fear of fire.
    /// </summary>
    [DataField]
    public SoundSpecifier? FireFearSound;
}

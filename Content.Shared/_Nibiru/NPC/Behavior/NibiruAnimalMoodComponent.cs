using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Nibiru.NPC.Behavior;

/// <summary>
/// Mood and loyalty of tamed animal.
/// If not fed or offended, the animal loses its mood and may go wild.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NibiruAnimalMoodComponent : Component
{
    /// <summary>
    /// Current mood (0..MaxMood).
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public float Mood = 75f;

    /// <summary>
    /// Maximum mood value.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float MaxMood = 100f;

    /// <summary>
    /// Speed of mood decrease per second.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float MoodDecayRate = 0.02f;

    /// <summary>
    /// Mood increase per feeding.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float MoodPerFeeding = 20f;

    /// <summary>
    /// Mood increase per petting.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float MoodPerPetting = 10f;

    /// <summary>
    /// Mood penalty when hit by owner.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float MoodPenaltyOnHit = 30f;

    /// <summary>
    /// Mood threshold below which the animal stops obeying commands.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float ObedienceThreshold = 25f;

    /// <summary>
    /// Mood threshold below which the animal can go wild.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float WildThreshold = 10f;

    /// <summary>
    /// Current qualitative state.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public AnimalMoodState MoodState = AnimalMoodState.Content;
}

[Serializable, NetSerializable]
public enum AnimalMoodState : byte
{
    /// <summary>
    /// Very happy. Increased obedience.
    /// </summary>
    Happy,

    /// <summary>
    /// Normal state.
    /// </summary>
    Content,

    /// <summary>
    /// Sad. Starts ignoring some commands.
    /// </summary>
    Sad,

    /// <summary>
    /// Angry. Does not obey commands, can bite.
    /// </summary>
    Angry,

    /// <summary>
    /// Gone wild. Complete loss of tameness.
    /// </summary>
    Wild
}

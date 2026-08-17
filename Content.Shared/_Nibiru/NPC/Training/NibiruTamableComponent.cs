using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Nibiru.NPC.Training;

[Serializable, NetSerializable]
public enum NibiruAnimalDiet : byte
{
    Herbivore,
    Carnivore,
    Omnivore
}

/// <summary>
/// Component for taming animals. Allows players to tame animals using food.
/// A tamed animal stops being afraid of/attacking its owner and their faction.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NibiruTamableComponent : Component
{
    /// <summary>
    /// Animal diet (determines what it can eat).
    /// </summary>
    [DataField("diet"), ViewVariables(VVAccess.ReadWrite)]
    public NibiruAnimalDiet Diet = NibiruAnimalDiet.Omnivore;

    /// <summary>
    /// List of favorite food IDs (give more trust).
    /// </summary>
    [DataField("favoriteFoods"), ViewVariables(VVAccess.ReadWrite)]
    public List<string> FavoriteFoods = new();

    /// <summary>
    /// List of favorite food tags.
    /// </summary>
    [DataField("favoriteFoodTags"), ViewVariables(VVAccess.ReadWrite)]
    public List<string> FavoriteFoodTags = new();
    #region Sounds

    /// <summary>
    /// Sound of feeding / eating.
    /// </summary>
    [DataField]
    public SoundSpecifier? FeedingSound;

    /// <summary>
    /// Sound of taming (happy sound).
    /// </summary>
    [DataField]
    public SoundSpecifier? TamedSound;

    /// <summary>
    /// Sound of following the owner (happy purring for cats, wagging tail for dogs).
    /// </summary>
    [DataField]
    public SoundSpecifier? FollowSound;

    #endregion
    /// <summary>
    /// Current trust level (0..MaxTrust).
    /// At TrustThreshold the animal is considered tamed.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public float TrustLevel;

    /// <summary>
    /// Trust threshold at which the animal is considered tamed.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float TrustThreshold = 100f;

    /// <summary>
    /// Maximum trust value.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float MaxTrust = 150f;

    /// <summary>
    /// How much trust one piece of suitable food gives.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float TrustPerFeeding = 15f;

    /// <summary>
    /// Rate of trust decay per second if the owner does not feed the animal.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float TrustDecayRate = 0.01f;

    /// <summary>
    /// Penalty for owner's aggression toward the pet.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float TrustPenaltyOnHit = 25f;

    /// <summary>
    /// List of acceptable food prototypes.
    /// If empty — the animal eats any food.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public List<string>? AcceptedFood;

    /// <summary>
    /// Has the animal been tamed at least once.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public bool IsTamed;

    /// <summary>
    /// Owner's EntityUid.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public EntityUid? OwnerUid;

    /// <summary>
    /// Can this animal be trained with commands (dogs, etc.).
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public bool Trainable;

    /// <summary>
    /// List of commands that THIS specific animal CAN be trained with.
    /// For example, a cat cannot be trained to Deliver, but a dog can learn Search.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public HashSet<NibiruAnimalCommand> PossibleCommands = new() { NibiruAnimalCommand.Follow, NibiruAnimalCommand.Stay };

    /// <summary>
    /// Learned commands.
    /// </summary>
    [DataField, ViewVariables]
    public HashSet<NibiruAnimalCommand> LearnedCommands = new();
}

/// <summary>
/// Commands that a tamed animal can be trained with.
/// </summary>
[Serializable, NetSerializable]
public enum NibiruAnimalCommand : byte
{
    /// <summary>
    /// Follow the owner.
    /// </summary>
    Follow,

    /// <summary>
    /// Stay in place.
    /// </summary>
    Stay,

    /// <summary>
    /// Attack the specified target.
    /// </summary>
    Attack,

    /// <summary>
    /// Grab the specified target and drag it.
    /// </summary>
    Grab,

    /// <summary>
    /// Growl/warn when strangers approach.
    /// </summary>
    Guard,

    /// <summary>
    /// Search for items by smell (for dogs).
    /// </summary>
    Search,

    /// <summary>
    /// Deliver items (for birds — letters).
    /// </summary>
    Deliver
}

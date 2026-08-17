using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Nibiru.NPC.Commands;

/// <summary>
/// Animal-specific abilities.
/// One animal can have multiple abilities.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NibiruAnimalAbilityComponent : Component
{
    #region Sounds

    /// <summary>
    /// Sound of growling/warning during guarding.
    /// </summary>
    [DataField]
    public SoundSpecifier? GrowlSound;

    #endregion
    /// <summary>
    /// List of available abilities for this animal.
    /// </summary>
    [DataField, ViewVariables]
    public HashSet<AnimalAbilityType> Abilities = new();

    /// <summary>
    /// Guard radius (for Guard ability). Growls when strangers approach.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float GuardRadius = 5f;

    /// <summary>
    /// Search radius (for Search ability).
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float SearchRadius = 15f;

    /// <summary>
    /// Max delivery range for birds.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float DeliveryRange = 50f;

    /// <summary>
    /// Can the animal carry an item (for delivering mail, carrying prey).
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public bool CanCarryItem;

    /// <summary>
    /// Current item carried by the animal.
    /// </summary>
    [ViewVariables]
    public EntityUid? CarriedItem;

    /// <summary>
    /// Ability cooldown in seconds.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float AbilityCooldown = 30f;

    /// <summary>
    /// Current cooldown timer.
    /// </summary>
    [ViewVariables]
    public float CooldownAccumulator;
}

/// <summary>
/// Animal ability types.
/// </summary>
[Serializable, NetSerializable]
public enum AnimalAbilityType : byte
{
    /// <summary>
    /// Guard: growls and warns when strangers approach.
    /// </summary>
    Guard,

    /// <summary>
    /// Search: tracks items or creatures by scent.
    /// </summary>
    Search,

    /// <summary>
    /// Delivery: birds carry mail/items over distance.
    /// </summary>
    Deliver,

    /// <summary>
    /// Pest control: cats catch mice and cockroaches.
    /// </summary>
    PestControl,

    /// <summary>
    /// Pack animal: can carry cargo.
    /// </summary>
    PackAnimal
}

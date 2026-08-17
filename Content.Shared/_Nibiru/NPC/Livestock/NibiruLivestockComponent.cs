using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared._Nibiru.NPC.Livestock;

/// <summary>
/// Livestock component: defines harvestable resources and breeding parameters.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NibiruLivestockComponent : Component
{
    #region Sounds

    /// <summary>
    /// Sound of shearing wool.
    /// </summary>
    [DataField]
    public SoundSpecifier? ShearingSound;

    /// <summary>
    /// Sound of milking.
    /// </summary>
    [DataField]
    public SoundSpecifier? MilkingSound;

    /// <summary>
    /// Sound of giving birth.
    /// </summary>
    [DataField]
    public SoundSpecifier? BirthSound;

    #endregion
    /// <summary>
    /// Resources that can be periodically harvested (wool, milk, etc.).
    /// </summary>
    [DataField, ViewVariables]
    public List<LivestockResource> HarvestableResources = new();

    /// <summary>
    /// Can this animal breed.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public bool CanBreed = true;

    /// <summary>
    /// Sex for breeding.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public LivestockSex Sex = LivestockSex.Female;

    /// <summary>
    /// Offspring prototype.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public string? OffspringPrototype;

    /// <summary>
    /// Gestation / incubation time in seconds.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float GestationTime = 300f;

    /// <summary>
    /// Current gestation timer (if pregnant).
    /// </summary>
    [ViewVariables]
    public float GestationAccumulator;

    /// <summary>
    /// How many offspring appear at once.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public int OffspringCount = 1;

    /// <summary>
    /// Maximum number of offspring at once.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public int MaxOffspringCount = 3;

    /// <summary>
    /// Is pregnant currently.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public bool IsPregnant;

    /// <summary>
    /// Breeding cooldown in seconds.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float BreedingCooldown = 600f;

    /// <summary>
    /// Breeding cooldown timer.
    /// </summary>
    [ViewVariables]
    public float BreedingCooldownAccumulator;

    /// <summary>
    /// Ready to breed.
    /// </summary>
    [ViewVariables]
    public bool ReadyToBreed => !IsPregnant && BreedingCooldownAccumulator <= 0f;

    /// <summary>
    /// Sprite for male.
    /// </summary>
    [DataField]
    public SpriteSpecifier? MaleSprite;

    /// <summary>
    /// Sprite for female.
    /// </summary>
    [DataField]
    public SpriteSpecifier? FemaleSprite;
}

/// <summary>
/// Description of the resource that can be harvested from the animal.
/// </summary>
[DataDefinition]
public sealed partial class LivestockResource
{
    /// <summary>
    /// Prototype of the item to be harvested.
    /// </summary>
    [DataField(required: true), ViewVariables(VVAccess.ReadWrite)]
    public string ItemPrototype = string.Empty;

    /// <summary>
    /// Time to accumulate resources in seconds.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float GrowthTime = 120f;

    /// <summary>
    /// Current progress of growth.
    /// </summary>
    [ViewVariables]
    public float GrowthAccumulator;

    /// <summary>
    /// Is the resource ready to harvest.
    /// </summary>
    [ViewVariables]
    public bool ReadyToHarvest => GrowthAccumulator >= GrowthTime;

    /// <summary>
    /// Quantity of items for one harvest.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public int Yield = 1;

    /// <summary>
    /// Is a tool required for harvesting (shears for shearing, etc.).
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public string? RequiredTool;
}

[Serializable, NetSerializable]
public enum LivestockSex : byte
{
    Male,
    Female
}

[Serializable, NetSerializable]
public enum LivestockVisuals : byte
{
    Sex,
    IsLeashed,
    BabyStage
}

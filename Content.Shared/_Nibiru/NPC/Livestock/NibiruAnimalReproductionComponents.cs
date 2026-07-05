using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Nibiru.NPC.Livestock;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NibiruAnimalSexComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public LivestockSex Sex = LivestockSex.Female;

    [DataField]
    public bool RandomizeOnMapInit = true;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class NibiruAnimalProductsComponent : Component
{
    [DataField, ViewVariables]
    public List<LivestockResource> HarvestableResources = new();
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NibiruAnimalBreederComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public bool Enabled = true;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public string? SpeciesId;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public string? OffspringPrototype;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float MateSearchRadius = 5f;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float BreedingCooldown = 600f;

    [ViewVariables, AutoNetworkedField]
    public float BreedingCooldownAccumulator;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float GestationTime = 300f;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public int MinOffspringCount = 1;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public int MaxOffspringCount = 3;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public NibiruAnimalGrowthSettings Growth = new();
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NibiruAnimalPregnancyComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public string? OffspringPrototype;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float GestationTime = 300f;

    [ViewVariables, AutoNetworkedField]
    public float GestationAccumulator;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public int MinOffspringCount = 1;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public int MaxOffspringCount = 3;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public NibiruAnimalGrowthSettings Growth = new();
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NibiruAnimalGrowthComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float GrowTime = 900f;

    [ViewVariables, AutoNetworkedField]
    public float Age;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float StartScale = 0.5f;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float AdultScale = 1f;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public string? AdultPrototype;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public List<NibiruAnimalGrowthModifierStep> ModifierSteps = new()
    {
        new() { Progress = 0.3f, Modifier = 0.2f },
        new() { Progress = 0.5f, Modifier = 0.5f },
        new() { Progress = 0.7f, Modifier = 0.8f },
    };

    [ViewVariables, AutoNetworkedField]
    public float CurrentModifier = 0.1f;
}

[DataDefinition]
public sealed partial class NibiruAnimalGrowthSettings
{
    [DataField]
    public bool AddGrowthComponent = true;

    [DataField]
    public float GrowTime = 900f;

    [DataField]
    public float StartScale = 0.5f;

    [DataField]
    public float AdultScale = 1f;

    [DataField]
    public string? AdultPrototype;

    [DataField]
    public List<NibiruAnimalGrowthModifierStep> ModifierSteps = new()
    {
        new() { Progress = 0.3f, Modifier = 0.2f },
        new() { Progress = 0.5f, Modifier = 0.5f },
        new() { Progress = 0.7f, Modifier = 0.8f },
    };
}

[DataDefinition]
public sealed partial class NibiruAnimalGrowthModifierStep
{
    [DataField(required: true)]
    public float Progress;

    [DataField(required: true)]
    public float Modifier;
}

[Serializable, NetSerializable]
public enum NibiruAnimalReproductionVisuals : byte
{
    Sex,
    GrowthModifier,
    GrowthProgress,
    IsPregnant,
}

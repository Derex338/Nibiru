
using Content.Shared.Lathe.Prototypes;
using Content.Shared.Lathe;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Content.Shared.Construction.Prototypes;

namespace Content.Shared._Nibiru.Workbench;

[RegisterComponent, NetworkedComponent]
public sealed partial class WorkbenchComponent : Component
{
    /// <summary>
    /// All of the recipe packs that the workbench has by default
    /// </summary>
    [DataField]
    public List<ProtoId<ConstructionPackPrototype>> StaticPacks = new();

    /// <summary>
    /// All of the recipe packs that the lathe is capable of researching
    /// </summary>
    [DataField]
    public List<ProtoId<ConstructionPackPrototype>> DynamicPacks = new();
    // Note that this shouldn't be modified dynamically.
    // I.e., this + the static recipies should represent all recipies that the lathe can ever make
    // Otherwise the material arbitrage test and/or LatheSystem.GetAllBaseRecipes needs to be updated

    /// <summary>
    /// The sound that plays when on the workbench producing an item
    /// </summary>
    [DataField]
    public SoundSpecifier? ProducingSound;
}


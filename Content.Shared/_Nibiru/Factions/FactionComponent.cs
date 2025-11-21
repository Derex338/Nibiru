using Content.Shared.Construction.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.GameStates;

namespace Content.Shared._Nibiru.Factions;

[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class FactionComponent : Component
{
    [AutoNetworkedField]
    [DataField("factionName")]
    public string FactionName { get; set; } = string.Empty;

    [AutoNetworkedField]
    [DataField("isCreator")]
    public bool IsCreator { get; set; } = false;

    /// <summary>
    /// All of the recipe packs that the faction type has by default
    /// </summary>
    [DataField]
    public List<ProtoId<ConstructionPackPrototype>> StaticPacks = new();

    [ViewVariables]
    public EntityUid? ResearchServer;

    [AutoNetworkedField]
    [ViewVariables]
    public List<EntityUid> Members { get; set; } = new();

    [AutoNetworkedField]
    [ViewVariables]
    public EntityUid Leader = default!;

    [AutoNetworkedField]
    [ViewVariables]
    public EntityUid Heir = default!;

    [AutoNetworkedField]
    [ViewVariables]
    public Color FactionColor = Color.Pink;
}

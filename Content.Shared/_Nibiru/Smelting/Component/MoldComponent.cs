using Content.Shared.Chemistry.Reagent;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Nibiru.Smelting;

[RegisterComponent, NetworkedComponent]
public sealed partial class MoldComponent : Component
{
    //[DataField(required: true)]
    //public string ResultEntity;

    [DataField(required: true)]
    public Dictionary<string, string> ResultEntities = new();

    [DataField]
    public bool DeleteAfterUse = false;

    [DataField]
    public string Slot = "";
}

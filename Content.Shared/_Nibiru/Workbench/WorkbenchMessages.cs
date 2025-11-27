
using Content.Shared.Construction;
using Content.Shared.Construction.Prototypes;
using Content.Shared.Lathe;
using Content.Shared.Research.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Nibiru.Workbench;

[NetSerializable, Serializable]
public enum WorkbenchUiKey
{
    Key,
}

[Serializable, NetSerializable]
public sealed class WorkbenchUpdateState : BoundUserInterfaceState
{
    public List<ProtoId<ConstructionPrototype>> Recipes;

    //public LatheRecipeBatch[] Queue;

    //public ProtoId<LatheRecipePrototype>? CurrentlyProducing;

    public WorkbenchUpdateState(List<ProtoId<ConstructionPrototype>> recipes)
    {
        Recipes = recipes;
    }
}

[Serializable, NetSerializable]
public sealed class RequestRecipesWorkbenchMessage : BoundUserInterfaceMessage
{

}

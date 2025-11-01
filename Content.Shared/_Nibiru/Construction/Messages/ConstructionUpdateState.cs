using Content.Shared.Research.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Content.Shared.Construction.Prototypes;

namespace Content.Shared.Construction.Messages;

[Serializable, NetSerializable]
public sealed class ConstructionUpdateState : BoundUserInterfaceState
{
    public List<ProtoId<ConstructionPrototype>> Recipes;

    //public List<ConstructionPrototype> Queue;

    //public ConstructionPrototype? CurrentlyProducing;

    public ConstructionUpdateState(List<ProtoId<ConstructionPrototype>> recipes)//, List<ConstructionPrototype> queue)//, ConstructionPrototype? currentlyProducing = null)
    {
        Recipes = recipes;
        //Queue = queue;
        //CurrentlyProducing = currentlyProducing;
    }
}

/// <summary>
///     Sent to the server when a client queues a new recipe.
/// </summary>
[Serializable, NetSerializable]
public sealed class ConstructionQueueRecipeMessage : BoundUserInterfaceMessage
{
    public readonly string ID;
    public readonly int Quantity;
    public ConstructionQueueRecipeMessage(string id, int quantity)
    {
        ID = id;
        Quantity = quantity;
    }
}

[NetSerializable, Serializable]
public enum ConstructionUiKey
{
    Key,
}
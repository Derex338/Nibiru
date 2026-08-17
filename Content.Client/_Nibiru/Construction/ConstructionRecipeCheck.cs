using Robust.Client.Player;
using Robust.Client.UserInterface;
using Content.Shared.Construction;
using Content.Shared.Construction.Prototypes;
using Robust.Shared.Prototypes;
using Content.Client.Construction;

namespace Content.Client._Nibiru.Construction;

public sealed partial class ConstructionRecipeCheck : EntitySystem
{
    [Dependency] private IPlayerManager _players = default!;

    public event Action<RecipeData>? OnConstructionRecipeUpdate;

    public override void Initialize()
    {
        base.Initialize();

        RequestRecipeInfo();

        SubscribeNetworkEvent<ConstructionCrafts>(OnConstructionCraftsEvent);
    }

    public void RequestRecipeInfo()
    {
        var entity = _players.LocalEntity;
        if (entity == null)
        {
            return;
        }

        RaiseNetworkEvent(new ConstructionUIOpen(GetNetEntity(entity.Value)));
    }

    private void OnConstructionCraftsEvent(ConstructionCrafts msg, EntitySessionEventArgs args)
    {
        var entity = GetEntity(msg.NetEntity);
        var data = new RecipeData(msg.Crafts);

        OnConstructionRecipeUpdate?.Invoke(data);
        EntityManager.System<ConstructionSystem>().UpdateRecipes(msg.Crafts);
    }

    public readonly record struct RecipeData(
        List<ProtoId<ConstructionPrototype>> crafts
    );
}

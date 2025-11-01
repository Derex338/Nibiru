using Robust.Client.Player;
using Robust.Client.UserInterface;
using Content.Shared.Construction;
using Content.Shared.Construction.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Client._Nibiru.Construction;

public sealed class ConstructionRecipeCheck : EntitySystem
{
    [Dependency] private readonly IPlayerManager _players = default!;

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
    }
	
	public readonly record struct RecipeData(
        List<ProtoId<ConstructionPrototype>> crafts
    );
/*
    public List<Control> GetCharacterInfoControls(EntityUid uid)
    {
        var ev = new GetCharacterInfoControlsEvent(uid);
        RaiseLocalEvent(uid, ref ev, true);
        return ev.Controls;
    }

    /// <summary>
    /// Event raised to get additional controls to display in the character info menu.
    /// </summary>
    [ByRefEvent]
    public readonly record struct GetCharacterInfoControlsEvent(EntityUid Entity)
    {
        public readonly List<Control> Controls = new();

        public readonly EntityUid Entity = Entity;
    }*/
}
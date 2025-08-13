using Content.Server.Popups;
using Content.Shared.ActionBlocker;
using Content.Shared.Input;
using Content.Shared.Interaction;
using Content.Shared.Rotatable;
using Content.Shared.Verbs;
using Robust.Shared.Input.Binding;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Utility;
using Content.Shared.Humanoid;
using Content.Shared._Nibiru.Factions;
using Robust.Server.GameObjects;

namespace Content.Server._Nibiru.Factions
{
	public sealed class AddFactionVerb : EntitySystem
	{
		[Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
	
		public override void Initialize()
		{
			base.Initialize();

			SubscribeLocalEvent<HumanoidAppearanceComponent, GetVerbsEvent<AlternativeVerb>>(AddToFactionVerb);
		}
	
		private void AddToFactionVerb(EntityUid uid, HumanoidAppearanceComponent component, GetVerbsEvent<AlternativeVerb> args)
		{
			if (args.Hands == null || !args.CanAccess || !args.CanInteract || args.Target == args.User)
				return;
		
			if (!HasComp<FactionComponent>(args.User) || !TryComp<FactionComponent>(args.User, out var Leader) || Leader.IsCreator != true)
			    return;
			
			if(HasComp<FactionComponent>(args.Target))
			{
				TryComp<FactionComponent>(args.Target, out var TargetFact);
				
				if(string.IsNullOrWhiteSpace(TargetFact?.FactionName) || string.IsNullOrWhiteSpace(Leader?.FactionName) || Leader?.FactionName == TargetFact?.FactionName)
					return;
			}

			AlternativeVerb verb = new()
			{
				Text = Loc.GetString("Пригласить во фракцию"),
				Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/pray.svg.png")),
				Act = () => RequestJoinFaction(args.User, (uid, component))
			};
			args.Verbs.Add(verb);
		}
	
		public void RequestJoinFaction(EntityUid user, Entity<HumanoidAppearanceComponent> target)
		{
			//if(!TryComp<FactionComponent>(user, out var Leader))
			//	return;
		
			//if (!EntityManager.TryGetComponent<FactionComponent>(target, out var factionComponent))
		    //{
		    //    factionComponent = EntityManager.AddComponent<FactionComponent>(target);
					
			//	factionComponent.FactionName = Leader.FactionName;
			//	factionComponent.IsCreator = false;
		    //}
			
			//_ui.OpenUi(target.Owner, StrippingUiKey.Key, user);
		}
	}
}
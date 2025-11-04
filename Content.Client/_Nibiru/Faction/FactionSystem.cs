using System;
using Robust.Shared.Serialization;
using Content.Shared._Nibiru.Factions;
using Content.Client.UserInterface.Systems.Faction.UI;
using Content.Client.UserInterface.Systems.Faction;

namespace Content.Client._Nibiru.Faction;

//костыль
public sealed class FactionSystem : EntitySystem
{
	public override void Initialize()
	{
		base.Initialize();
	}
		
	public void RequestState(FactionMenu? window)
	{
		if(window == null)
			return;
			
		var msg = new FactionStateRequestMessage();
		
		RaiseNetworkEvent(msg);
			
		if(msg.creator != null
		&& msg.creator == true)
		{
			window.FactionCreate.Visible = false;
			window.FactionLeaderWindow.Visible = !window.FactionCreate.Visible;
		}
	}
}

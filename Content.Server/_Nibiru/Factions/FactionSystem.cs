using Content.Shared._Nibiru.Factions;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Player;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Prototypes;
using Content.Server.Mind;
using Content.Server.Popups;
using Content.Server.EUI;
using Content.Shared.IdentityManagement;
using Content.Shared.Research.Components;
using Content.Shared.Construction.Prototypes;
using Robust.Shared.Map;
using System.Linq;
using Content.Server.Popups;
using Content.Server.Administration.Logs;
using Content.Shared.Database;

namespace Content.Server._Nibiru.Factions;

    public sealed class FactionSystem : EntitySystem
    {	
		[Dependency] private readonly IPrototypeManager _prototypeManager = default!;
		[Dependency] private readonly EntityLookupSystem _lookup = default!;
		[Dependency] private readonly PopupSystem _popup = default!;
		[Dependency] private readonly IAdminLogManager _adminLog = default!;
		
		private static readonly HashSet<Entity<FactionComponent>> ClientLookup = new();
	
        public override void Initialize()
        {
            base.Initialize();
            SubscribeNetworkEvent<FactionCreateRequestMessage>(OnFactionCreateRequest);
        }

        private void OnFactionCreateRequest(FactionCreateRequestMessage msg, EntitySessionEventArgs args)
        {
            // Проверяем, что имя фракции корректное
            if (string.IsNullOrWhiteSpace(msg.FactionName))
            {
                return;
            }

            var player = args.SenderSession.AttachedEntity;
			
			if (!player.HasValue)
            return;
		
			var allFactions = GetFactions(player.Value).ToList();

			foreach(var faction in allFactions)
			{
				if(EntityManager.TryGetComponent<FactionComponent>(faction, out var factionComponent)
					&& factionComponent.FactionName == msg.FactionName)
				{
					_popup.PopupEntity(
					Loc.GetString("faction-already-exist", ("factionName", factionComponent.FactionName)),
					player.Value,
					player.Value);
					return;
				}
			}

            // Создаём фракцию
            CreateFaction(player.Value, msg.FactionName);
        }

        private void CreateFaction(EntityUid player, string factionName)
        {
            // Выдаём компонент фракции
            if (!EntityManager.TryGetComponent<FactionComponent>(player, out var factionComponent))
            {
                factionComponent = EntityManager.AddComponent<FactionComponent>(player);
				
				factionComponent.FactionName = factionName;
				factionComponent.IsCreator = true;
				
				_adminLog.Add(LogType.FactionCreated, LogImpact.Medium, $"{ToPrettyString(player):player} создал фракцию с названием {factionName}");
				
				foreach (var recipe in _prototypeManager.EnumeratePrototypes<ConstructionPackPrototype>())
				{
					factionComponent.StaticPacks.Add(recipe.ID);
				}
            }
        }
		
		public HashSet<Entity<FactionComponent>> GetFactions(EntityUid client)
        {
            ClientLookup.Clear();

            var clientXform = Transform(client);
            if (clientXform.GridUid is not { } grid)
                return ClientLookup;

            _lookup.GetGridEntities(grid, ClientLookup);
            return ClientLookup;
        }
    }

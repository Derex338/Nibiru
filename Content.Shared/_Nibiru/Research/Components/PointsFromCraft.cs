using Content.Shared.Construction;
using JetBrains.Annotations;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Content.Shared._Nibiru.Factions;
using Robust.Shared.Map;
using System.Linq;
using Content.Shared.Research.Components;

namespace Content.Server.Construction.Completions
{
    [UsedImplicitly]
    [DataDefinition]
    public sealed partial class PointsFromCraft : EntitySystem, IGraphAction
    {	
		[DataField("points")] public int Points { get; private set; } = 100;
	
		private static readonly HashSet<Entity<TechnologyDatabaseComponent>> ClientLookup = new();
	
        public void PerformAction(EntityUid uid, EntityUid? userUid, IEntityManager entityManager)
        {
            if (userUid == null)
                return;

			if(entityManager.TryGetComponent<FactionComponent>(userUid, out var user)
			&& entityManager.TryGetComponent<FactionComponent>(user.ResearchServer, out var server)
			&& entityManager.TryGetComponent<ResearchServerComponent>(user.ResearchServer, out var research)
			&& server.FactionName == user.FactionName)
			{
				research.Points += Points;
			}
        }
    }
}
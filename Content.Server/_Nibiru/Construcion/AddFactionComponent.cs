using Content.Server.Popups;
using Content.Shared.Construction;
using JetBrains.Annotations;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Content.Shared._Nibiru.Factions;

namespace Content.Server.Construction.Completions
{
    [UsedImplicitly]
    [DataDefinition]
    public sealed partial class AddFactionComponent : IGraphAction
    {	
        public void PerformAction(EntityUid uid, EntityUid? userUid, IEntityManager entityManager)
        {
            if (userUid == null)
                return;

			if(entityManager.TryGetComponent<FactionComponent>(userUid, out var user)
			&& !entityManager.TryGetComponent<FactionComponent>(uid, out var construction))
			{
				construction = entityManager.AddComponent<FactionComponent>(uid);
				
				construction.FactionName = user.FactionName;
			}
        }
    }
}

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

			entityManager.EntitySysManager.GetEntitySystem<ConstructionSystem>().AddFactionComp(uid, userUid.Value);
        }
    }
}

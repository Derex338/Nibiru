using Content.Shared.Construction;
using Content.Shared._Nibiru.Factions;
using JetBrains.Annotations;
using Robust.Shared.GameObjects;

namespace Content.Server._Nibiru.Factions.Systems;

[UsedImplicitly]
[DataDefinition]
public sealed partial class SetFactionStatue : IGraphAction
{
    public void PerformAction(EntityUid uid, EntityUid? userUid, IEntityManager entityManager)
    {
        if (userUid == null)
            return;

        if (!entityManager.TryGetComponent<FactionComponent>(userUid.Value, out var factionComp))
            return;

        if (string.IsNullOrEmpty(factionComp.FactionName))
            return;

        var statue = entityManager.EnsureComponent<FactionStatueComponent>(uid);
        statue.FactionName = factionComp.FactionName;
        statue.AllMembers = new List<FactionMemberRecord>(factionComp.AllMembers);
        statue.Builder = userUid.Value;

        entityManager.Dirty(uid, statue);

        var system = entityManager.EntitySysManager.GetEntitySystem<FactionStatueSystem>();
        system.OpenSelectionEui(uid, statue);
    }
}

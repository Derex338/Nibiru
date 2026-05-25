using Content.Shared.Construction;
using Content.Shared._Nibiru.Factions;
using JetBrains.Annotations;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.GameObjects;

namespace Content.Server._Nibiru.Factions.Systems;

[UsedImplicitly]
[DataDefinition]
public sealed partial class SetFactionVisuals : IGraphAction
{


    public void PerformAction(EntityUid uid, EntityUid? userUid, IEntityManager entityManager)
    {
        if (userUid == null)
            return;

        if (!entityManager.TryGetComponent<FactionComponent>(userUid.Value, out var factionComp))
            return;

        if (string.IsNullOrEmpty(factionComp.FactionName))
            return;

        var visuals = entityManager.EnsureComponent<FactionVisualsComponent>(uid);
        visuals.FactionName = factionComp.FactionName;
        visuals.LogoBackground = factionComp.LogoBackground;
        
        // Копируем пиксели, чтобы они сохранились навсегда и не менялись при изменении логотипа фракции
        if (factionComp.LogoPixels != null && factionComp.LogoPixels.Count == 16 * 16)
        {
            visuals.LogoPixels = new List<Robust.Shared.Maths.Color>(factionComp.LogoPixels);
        }



        entityManager.Dirty(uid, visuals);
    }
}

using Content.Shared.Popups;
using Content.Shared.Research.Components;
using Content.Shared.Research.Prototypes;
using Robust.Shared.Prototypes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Content.Server.Research.Systems;
public sealed partial class ResearchSystem
{
    private void InitializeEpoch()
    {
    }

    public void CheckEpochUnlock(EntityUid uid, TechnologyPrototype tech, TechnologyDatabaseComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        var epochs = PrototypeManager.EnumeratePrototypes<ResearchEpochPrototype>()
            .OrderBy(e => e.Order)
            .ToList();

        foreach (var epoch in epochs)
        {
            if (epoch.UnlockNextEpochTech != null && tech.ID == epoch.UnlockNextEpochTech)
            {
                var nextEpoch = epochs.FirstOrDefault(e => e.Order == epoch.Order + 1);
                if (epoch != null && !component.UnlockedEpochs.Contains(epoch.ID))
                {
                    component.UnlockedEpochs.Add(epoch.ID);
                    Dirty(uid, component);

                    _popup.PopupEntity(
                        Loc.GetString("research-epoch-unlocked", ("epoch", Loc.GetString(epoch.Name))),
                        uid,
                        PopupType.Large
                    );

                    if (TryComp<ResearchServerComponent>(uid, out var server))
                    {
                        foreach (var client in server.Clients)
                        {
                            var ev = new TechnologyDatabaseModifiedEvent(null);
                            RaiseLocalEvent(client, ref ev);
                        }
                    }
                }
                break;
            }
        }
    }
}

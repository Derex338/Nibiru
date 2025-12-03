using Content.Shared.Popups;
using Content.Shared.Research.Components;
using Content.Shared.Research.Prototypes;
using Robust.Client.UserInterface.Controls;
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
        //SubscribeLocalEvent<TechnologyDatabaseComponent, MapInitEvent>(OnEpochMapInit);
    }

    //private void OnEpochMapInit(EntityUid uid, TechnologyDatabaseComponent component, MapInitEvent args)
    //{
    //    // Инициализируем первую эпоху, если список пуст
    //    if (component.UnlockedEpochs.Count == 0)
    //    {
    //        component.UnlockedEpochs.Add("EpochStone");
    //        component.CurrentEpoch = "EpochStone";
    //        Dirty(uid, component);
    //    }
    //}

    /// <summary>
    /// Проверяет, открыло ли исследование новую эпоху
    /// </summary>
    public void CheckEpochUnlock(EntityUid uid, TechnologyPrototype tech, TechnologyDatabaseComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        // Получаем все эпохи по порядку
        var epochs = PrototypeManager.EnumeratePrototypes<ResearchEpochPrototype>()
            .OrderBy(e => e.Order)
            .ToList();

        foreach (var epoch in epochs)
        {
            // Пропускаем уже разблокированные
            //if (component.UnlockedEpochs.Contains(epoch.ID))
            //    continue;

            // Проверяем, открывает ли эта технология следующую эпоху
            if (epoch.UnlockNextEpochTech != null && tech.ID == epoch.UnlockNextEpochTech)
            {
                // Находим следующую эпоху
                var nextEpoch = epochs.FirstOrDefault(e => e.Order == epoch.Order + 1);
                if (epoch != null && !component.UnlockedEpochs.Contains(epoch.ID))
                {
                    component.UnlockedEpochs.Add(epoch.ID);
                    Dirty(uid, component);

                    // Уведомляем игроков
                    _popup.PopupEntity(
                        Loc.GetString("research-epoch-unlocked", ("epoch", Loc.GetString(epoch.Name))),
                        uid,
                        PopupType.Large
                    );

                    // Синхронизируем с клиентами
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

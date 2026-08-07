using Content.Server.EUI;
using Content.Shared._Nibiru.Factions;
using Robust.Shared.Player;

namespace Content.Server._Nibiru.Factions.Systems;

/// <summary>
/// Обрабатывает постройку статуй и открытие EUI выбора члена фракции.
/// </summary>
public sealed partial class FactionStatueSystem : EntitySystem
{
[Dependency] private EuiManager _euiManager = default!;

    /// <summary>
    /// Открывает окно выбора члена фракции для статуи.
    /// </summary>
    public void OpenSelectionEui(EntityUid statueUid, FactionStatueComponent component)
    {
        if (component.Builder == null)
            return;

        // Получаем сессию строителя
        if (!TryComp<ActorComponent>(component.Builder.Value, out var actor))
            return;

        var eui = new UI.FactionStatueSelectionEui(statueUid);
        _euiManager.OpenEui(eui, actor.PlayerSession);
    }
}

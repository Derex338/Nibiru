using Content.Server.EUI;
using Content.Shared._Nibiru.Factions;
using Robust.Shared.Player;

namespace Content.Server._Nibiru.Factions.Systems;

public sealed partial class FactionStatueSystem : EntitySystem
{
    [Dependency] private EuiManager _euiManager = default!;

    /// <summary>
    /// Open faction selection EUI for statue.
    /// </summary>
    public void OpenSelectionEui(EntityUid statueUid, FactionStatueComponent component)
    {
        if (component.Builder == null)
            return;

        // Get builder session
        if (!TryComp<ActorComponent>(component.Builder.Value, out var actor))
            return;

        var eui = new UI.FactionStatueSelectionEui(statueUid);
        _euiManager.OpenEui(eui, actor.PlayerSession);
    }
}

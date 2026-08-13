using Content.Client._Nibiru.LanguageSelect.UI;
using Content.Client.Lobby;
using Content.Shared.CCVar;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Configuration;

namespace Content.Client._Nibiru.LanguageSelect;

/// <summary>
/// Shows a language selection dialog on the first time a player enters the lobby.
/// The selection is persisted via <see cref="CCVars.NibiruLanguageSelected"/> and
/// <see cref="Robust.Shared.CVars.LocCultureName"/> CVars.
/// </summary>
public sealed partial class LanguageSelectUIController : UIController, IOnStateEntered<LobbyState>
{
    [Dependency] private IConfigurationManager _cfg = default!;

    private LanguageSelectWindow? _window;

    public void OnStateEntered(LobbyState state)
    {
        // Only show the dialog if the player hasn't chosen a language before
        if (_cfg.GetCVar(CCVars.NibiruLanguageSelected))
            return;

        if (_window is { IsOpen: true })
            return;

        _window = new LanguageSelectWindow();
        _window.OpenCentered();
    }
}

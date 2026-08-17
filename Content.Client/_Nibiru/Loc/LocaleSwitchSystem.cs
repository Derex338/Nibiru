using System;
using System.Globalization;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Log;
using Robust.Client.State;
using Content.Shared.CCVar;
using Content.Shared.Localizations;
using Content.Client.UserInterface.Systems.EscapeMenu;
using Content.Client.Gameplay;
using Content.Client.Lobby;

namespace Content.Client.Localization
{
    /// <summary>
    /// Client system for switching languages.
    /// Throws <see cref="LanguageChangedEvent"/> into EventBus (EventSource.Local)
    /// and notifies all registered ILanguageRefreshable components.
    /// </summary>
    public sealed partial class LocaleSwitchSystem : EntitySystem
    {
        [Dependency] private IConfigurationManager _cfg = default!;
        [Dependency] private IUserInterfaceManager _ui = default!;
        [Dependency] private ILocalizationManager _loc = default!;
        [Dependency] private IStateManager _stateManager = default!;

        private ISawmill _sawmill = default!;

        public override void Initialize()
        {
            base.Initialize();

            _sawmill = Logger.GetSawmill("locale");

            _cfg.OnValueChanged(CCVars.ClientLanguage, OnLanguageChanged, invokeImmediately: false);
        }

        public override void Shutdown()
        {
            base.Shutdown();
            _cfg.UnsubValueChanged(CCVars.ClientLanguage, OnLanguageChanged);
        }

        public void SwitchLanguage(string cultureCode)
        {
            _cfg.SetCVar(CCVars.ClientLanguage, cultureCode);
        }

        private void OnLanguageChanged(string cultureCode)
        {
            var oldCultureCode = _loc.DefaultCulture?.Name ?? "en-US";

            CultureInfo culture;
            try
            {
                culture = CultureInfo.GetCultureInfo(cultureCode, predefinedOnly: false);
            }
            catch (Exception e)
            {
                _sawmill.Error($"Unknown culture code: {cultureCode}. Error: {e}");
                return;
            }

            if (!TrySetEngineCulture(culture))
            {
                _sawmill.Error("Failed to switch culture in ILocalizationManager.");
                return;
            }

            try
            {
                _loc.ReloadLocalizations();
            }
            catch (Exception e)
            {
                _sawmill.Error($"Error reloading localizations: {e}");
            }

            // 1. Close all open windows that cannot update themselves
            CloseAllOpenWindows();

            // 2. Send event to all EntitySystems
            var ev = new LanguageChangedEvent(oldCultureCode, cultureCode);
            RaiseLocalEvent(ev);

            // 3. Notify all ILanguageRefreshable (UIControllers)
            LanguageRefreshManager.RefreshAll();

            // 4. Reload current state UI
            ReloadCurrentState();

            // 5. Recreate Settings and ESC menu windows
            ReloadUIControllers();

            // 6. Show language changed notification
            NotifyLanguageChanged();

            _cfg.SaveToFile();
        }

        private void ReloadCurrentState()
        {
            var currentState = _stateManager.CurrentState;
            if (currentState == null)
                return;

            if (currentState is GameplayState gameplayState)
            {
                gameplayState.ReloadMainScreen();
                return;
            }

            // For other states (LobbyState, MainScreen, LauncherConnecting)
            // switch to LanguageSwitchDummyState and back.
            // RequestStateChange with the same type does nothing, so intermediate is needed.
            try
            {
                var stateType = currentState.GetType();
                _stateManager.RequestStateChange<LanguageSwitchDummyState>();
                _stateManager.RequestStateChange(stateType);
            }
            catch (Exception e)
            {
                _sawmill.Error($"Error restarting active UI state: {e}");
            }
        }

        private void ReloadUIControllers()
        {
            try
            {
                var optionsController = _ui.GetUIController<OptionsUIController>();
                optionsController.ReloadWindow();
            }
            catch (Exception e)
            {
                _sawmill.Error($"Error restarting OptionsUIController: {e}");
            }

            try
            {
                var escapeController = _ui.GetUIController<EscapeUIController>();
                escapeController.ReloadWindow();
            }
            catch (Exception e)
            {
                _sawmill.Error($"Error restarting EscapeUIController: {e}");
            }
        }

        private bool TrySetEngineCulture(CultureInfo culture)
        {
            try
            {
                _loc.SetCulture(culture);
                return true;
            }
            catch (Exception e)
            {
                _sawmill.Error($"Error calling SetCulture: {e}");
                return false;
            }
        }

        /// <summary>
        /// Closes all open windows (BaseWindow) except those we recreate ourselves.
        /// </summary>
        private void CloseAllOpenWindows()
        {
            try
            {
                // WindowRoot contains all open BaseWindow.
                // Collect into a list because Close() removes from the collection.
                var windows = _ui.WindowRoot.Children;
                var toClose = new System.Collections.Generic.List<Robust.Client.UserInterface.CustomControls.BaseWindow>();
                foreach (var child in windows)
                {
                    if (child is Robust.Client.UserInterface.CustomControls.BaseWindow bw)
                    {
                        toClose.Add(bw);
                    }
                }

                foreach (var bw in toClose)
                {
                    try
                    {
                        bw.Close();
                    }
                    catch (Exception e)
                    {
                        _sawmill.Error($"Error closing window {bw.GetType().Name}: {e}");
                    }
                }
            }
            catch (Exception e)
            {
                _sawmill.Error($"Error iterating windows: {e}");
            }
        }

        /// <summary>
        /// Shows language changed notification.
        /// </summary>
        private void NotifyLanguageChanged()
        {
            try
            {
                var cultureName = _loc.DefaultCulture?.NativeName ?? "unknown";
                var msg = Loc.GetString("nibiru-locale-switched", ("language", cultureName));
                var hint = Loc.GetString("nibiru-locale-reconnect-hint");
                _ui.Popup(msg + "\n" + hint, Loc.GetString("nibiru-locale-switched-title"));
            }
            catch (Exception e)
            {
                _sawmill.Error($"Error showing notification: {e}");
            }
        }
    }

}

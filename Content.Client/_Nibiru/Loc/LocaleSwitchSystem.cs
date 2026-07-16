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
    /// Клиентская система переключения языка.
    /// Бросает <see cref="LanguageChangedEvent"/> в EventBus (EventSource.Local)
    /// и оповещает все зарегистрированные ILanguageRefreshable компоненты.
    /// </summary>
    public sealed class LocaleSwitchSystem : EntitySystem
    {
        [Dependency] private readonly IConfigurationManager _cfg = default!;
        [Dependency] private readonly IUserInterfaceManager _ui = default!;
        [Dependency] private readonly ILocalizationManager _loc = default!;
        [Dependency] private readonly IStateManager _stateManager = default!;

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
                _sawmill.Error($"Неизвестный код культуры: {cultureCode}. Ошибка: {e}");
                return;
            }

            if (!TrySetEngineCulture(culture))
            {
                _sawmill.Error("Не удалось переключить культуру в ILocalizationManager.");
                return;
            }

            try
            {
                _loc.ReloadLocalizations();
            }
            catch (Exception e)
            {
                _sawmill.Error($"Ошибка при перезагрузке локализаций: {e}");
            }

            // 1. Закрываем все открытые окна, которые не умеют обновляться
            CloseAllOpenWindows();

            // 2. Шлём событие всем EntitySystem'ам
            var ev = new LanguageChangedEvent(oldCultureCode, cultureCode);
            RaiseLocalEvent(ev);

            // 3. Оповещаем все ILanguageRefreshable (UIController'ы)
            LanguageRefreshManager.RefreshAll();

            // 4. Перезагружаем UI текущего состояния
            ReloadCurrentState();

            // 5. Пересоздаём окна настроек и ESC-меню
            ReloadUIControllers();

            // 6. Показываем подсказку что язык сменился
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

            // Для остальных состояний (LobbyState, MainScreen, LauncherConnecting)
            // переключаемся на LanguageSwitchDummyState и обратно.
            // RequestStateChange с тем же типом — no-op, поэтому нужен промежуточный.
            try
            {
                var stateType = currentState.GetType();
                _stateManager.RequestStateChange<LanguageSwitchDummyState>();
                _stateManager.RequestStateChange(stateType);
            }
            catch (Exception e)
            {
                _sawmill.Error($"Ошибка при перезапуске активного UI состояния: {e}");
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
                _sawmill.Error($"Ошибка при перезапуске OptionsUIController: {e}");
            }

            try
            {
                var escapeController = _ui.GetUIController<EscapeUIController>();
                escapeController.ReloadWindow();
            }
            catch (Exception e)
            {
                _sawmill.Error($"Ошибка при перезапуске EscapeUIController: {e}");
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
                _sawmill.Error($"Ошибка при вызове SetCulture: {e}");
                return false;
            }
        }

        /// <summary>
        /// Закрывает все открытые окна (BaseWindow) кроме тех, что мы пересоздаём сами.
        /// </summary>
        private void CloseAllOpenWindows()
        {
            try
            {
                // WindowRoot содержит все открытые BaseWindow.
                // Собираем в список т.к. Close() удаляет из коллекции.
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
                        _sawmill.Error($"Ошибка при закрытии окна {bw.GetType().Name}: {e}");
                    }
                }
            }
            catch (Exception e)
            {
                _sawmill.Error($"Ошибка при обходе окон: {e}");
            }
        }

        /// <summary>
        /// Показывает уведомление о смене языка.
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
                _sawmill.Error($"Ошибка при показе уведомления: {e}");
            }
        }
    }

}

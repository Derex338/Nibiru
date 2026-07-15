using System;
using System.Globalization;
using System.Reflection;
using Robust.Client.UserInterface;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Log;

namespace Content.Client.Localization
{
    /// <summary>
    /// Клиентская система, которая:
    ///  1) слушает CVar ui.language;
    ///  2) переключает культуру в ILocalizationManager;
    ///  3) обходит текущее дерево UI и обновляет все ILocalizedControl;
    ///  4) сразу сохраняет конфиг на диск, чтобы выбор пережил краш/kill процесса,
    ///     а не только штатное закрытие игры.
    ///
    /// ВАЖНО: EntitySystem-наследники в Robust.Toolbox подхватываются автоматически
    /// через рефлексию при старте клиента — регистрировать этот класс где-либо
    /// дополнительно не нужно.
    /// </summary>
    public sealed class LocaleSwitchSystem : EntitySystem
    {
        [Dependency] private readonly IConfigurationManager _cfg = default!;
        [Dependency] private readonly IUserInterfaceManager _ui = default!;
        [Dependency] private readonly ILocalizationManager _loc = default!;

        private ISawmill _sawmill = default!;

        public override void Initialize()
        {
            base.Initialize();

            _sawmill = Logger.GetSawmill("locale");

            // invokeImmediately: true - применяем сохранённый ранее язык сразу при загрузке клиента
            _cfg.OnValueChanged(CVars.ClientLanguage, OnLanguageChanged, invokeImmediately: true);
        }

        public override void Shutdown()
        {
            base.Shutdown();
            _cfg.UnsubValueChanged(CVars.ClientLanguage, OnLanguageChanged);
        }

        /// <summary>
        /// Публичный метод, который дёргает ваше меню опций при выборе языка в списке.
        /// Пример вызова из кода опций:
        ///     _cfgMan.SetCVar(LocaleCVars.ClientLanguage, "ru-RU");
        /// (не обязательно вызывать этот метод напрямую - достаточно поменять CVar,
        /// система сама подхватит изменение через OnValueChanged)
        /// </summary>
        public void SwitchLanguage(string cultureCode)
        {
            _cfg.SetCVar(CVars.ClientLanguage, cultureCode);
        }

        private void OnLanguageChanged(string cultureCode)
        {
            CultureInfo culture;
            try
            {
                culture = new CultureInfo(cultureCode);
            }
            catch (CultureNotFoundException)
            {
                _sawmill.Error($"Неизвестный код культуры: {cultureCode}");
                return;
            }

            if (!TrySetEngineCulture(culture))
            {
                _sawmill.Error(
                    "Не удалось переключить культуру в ILocalizationManager. " +
                    "Проверьте актуальную сигнатуру SetCulture/LoadCulture в вашей версии движка " +
                    "(Robust.Shared.Localization.ILocalizationManager) - см. примечание в README.");
                return;
            }

            // Обновляем всё, что сейчас реально показано на экране.
            if (_ui.RootControl != null)
                LocalizedControlTreeWalker.RefreshTree(_ui.RootControl);

            // ARCHIVE-cvar-ы по умолчанию пишутся на диск при штатном закрытии клиента.
            // Сохраняем сразу, чтобы выбор не потерялся при аварийном завершении.
            _cfg.SaveToFile();
        }

        /// <summary>
        /// В разных версиях движка публичный API переключения культуры мог называться
        /// по-разному (SetCulture / LoadCulture / Culture-setter). Пробуем сначала
        /// нормальный публичный вызов, и только если сигнатуры не совпали -
        /// падаем на рефлексию как страховку, НЕ трогая исходники движка.
        /// </summary>
        private bool TrySetEngineCulture(CultureInfo culture)
        {
            // --- Вариант 1: штатный публичный API (проверьте у себя через автокомплит IDE) ---
            try
            {
                // Если у вас есть метод "загрузить .ftl для этой культуры, если ещё не грузили":
                var loadMethod = typeof(ILocalizationManager).GetMethod("LoadCulture", new[] { typeof(CultureInfo) });
                loadMethod?.Invoke(_loc, new object[] { culture });

                var setMethod = typeof(ILocalizationManager).GetMethod("SetCulture", new[] { typeof(CultureInfo) });
                if (setMethod != null)
                {
                    setMethod.Invoke(_loc, new object[] { culture });
                    return true;
                }
            }
            catch (Exception e)
            {
                _sawmill.Error($"Ошибка при вызове публичного API смены культуры: {e}");
            }

            // --- Вариант 2: страховка через reflection на случай внутреннего/скрытого API ---
            try
            {
                var internalField = _loc.GetType().GetField("_currentCulture",
                    BindingFlags.NonPublic | BindingFlags.Instance);

                if (internalField != null)
                {
                    internalField.SetValue(_loc, culture);
                    return true;
                }
            }
            catch (Exception e)
            {
                _sawmill.Error($"Reflection fallback тоже не сработал: {e}");
            }

            return false;
        }
    }
}

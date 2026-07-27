using System;
using System.Collections.Generic;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Localization;

namespace Content.Client.Localization
{
    /// <summary>
    /// Marks a control as capable of re-reading its text from ILocalizationManager
    /// after a language change. This is purely a content interface, the engine knows nothing about it
    /// and should not — LocaleSwitchSystem finds such controls by walking the tree.
    /// </summary>
    public interface ILocalizedControl
    {
        void RefreshLocalization();
    }

    /// <summary>
    /// Замена обычному Label для ЛЮБОГО статического текста, который берётся из
    /// локализации напрямую (не через каждый Update()). Используйте вместо Label
    /// везде, где выставляете Text = Loc.GetString(...) один раз при создании окна.
    ///
    /// Пример:
    ///   var label = new LocLabel();
    ///   label.SetLoc("some-window-title");
    /// вместо:
    ///   var label = new Label { Text = Loc.GetString("some-window-title") };
    /// </summary>
    public sealed class LocLabel : Label, ILocalizedControl
    {
        private string? _locId;
        private (string, object)[] _args = Array.Empty<(string, object)>();

        /// <param name="locId">Fluent id строки.</param>
        /// <param name="args">Аргументы Fluent-подстановки, как в Loc.GetString.</param>
        public void SetLoc(string locId, params (string, object)[] args)
        {
            _locId = locId;
            _args = args;
            RefreshLocalization();
        }

        public void RefreshLocalization()
        {
            if (_locId is null)
                return;

            Text = IoCManager.Resolve<ILocalizationManager>().GetString(_locId, _args);
        }
    }

    /// <summary>
    /// То же самое, но для RichTextLabel (часто используется под markup-разметку).
    /// </summary>
    public sealed class LocRichTextLabel : RichTextLabel, ILocalizedControl
    {
        private string? _locId;
        private (string, object)[] _args = Array.Empty<(string, object)>();

        public void SetLoc(string locId, params (string, object)[] args)
        {
            _locId = locId;
            _args = args;
            RefreshLocalization();
        }

        public void RefreshLocalization()
        {
            if (_locId is null)
                return;

            SetMessage(IoCManager.Resolve<ILocalizationManager>().GetString(_locId, _args));
        }
    }

    /// <summary>
    /// Замена Button для случаев, когда текст кнопки статический
    /// (не переключается вручную кодом при клике, как в вашем примере с кнопкой-тумблером —
    /// для таких кнопок ничего переписывать и не нужно, они и так работали).
    /// </summary>
    public sealed class LocButton : Button, ILocalizedControl
    {
        private string? _locId;
        private (string, object)[] _args = Array.Empty<(string, object)>();

        public void SetLoc(string locId, params (string, object)[] args)
        {
            _locId = locId;
            _args = args;
            RefreshLocalization();
        }

        public void RefreshLocalization()
        {
            if (_locId is null)
                return;

            Text = IoCManager.Resolve<ILocalizationManager>().GetString(_locId, _args);
        }
    }

    /// <summary>
    /// Утилита обхода дерева контролов — используется системой смены языка,
    /// но можно вызывать и вручную (например, только для конкретного окна сразу после его создания,
    /// если вы открыли его ДО смены языка и хотите принудительно обновить).
    /// </summary>
    public static class LocalizedControlTreeWalker
    {
        public static void RefreshTree(Control root)
        {
            var stack = new Stack<Control>();
            stack.Push(root);

            while (stack.Count > 0)
            {
                var control = stack.Pop();

                if (control is ILocalizedControl localized)
                    localized.RefreshLocalization();

                foreach (var child in control.Children)
                    stack.Push(child);
            }
        }
    }
}

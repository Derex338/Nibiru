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
    /// Replacement for regular Label for ANY static text taken directly from
    /// localization (not through every Update()). Use instead of Label
    /// wherever you set Text = Loc.GetString(...) once when creating the window.
    /// </summary>
    public sealed class LocLabel : Label, ILocalizedControl
    {
        private string? _locId;
        private (string, object)[] _args = Array.Empty<(string, object)>();

        /// <param name="locId">Fluent string ID.</param>
        /// <param name="args">Arguments for Fluent substitution, as in Loc.GetString.</param>
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
    /// Same, but for RichTextLabel (often used under markup).
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
    /// Replacement for Button for cases where the button text is static
    /// (not manually switched by code on click, as in your toggle button example —
    /// such buttons didn't need rewriting, they worked as-is).
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
    /// Control tree traversal utility — used by the language switching system,
    /// but can also be called manually (for example, only for a specific window right after it's created,
    /// if you opened it BEFORE the language change and want to force an update).
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

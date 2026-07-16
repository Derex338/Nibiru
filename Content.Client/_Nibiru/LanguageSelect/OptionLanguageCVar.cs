using System.Globalization;
using Content.Client.Options.UI;
using Robust.Client.UserInterface.Controls;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.Localization;

namespace Content.Client._Nibiru.LanguageSelect;

/// <summary>
/// A settings option that controls the interface language via <see cref="CVars.LocCultureName"/>.
/// Unlike the standard <see cref="OptionDropDownCVar{T}"/>, this immediately applies
/// the selected culture via <see cref="ILocalizationManager"/> when the user saves.
/// </summary>
public sealed class OptionLanguageCVar : BaseOptionCVar<string>
{
    private readonly ILocalizationManager _loc;
    private readonly OptionDropDown _dropDown;
    private readonly ItemEntry[] _entries;

    protected override string Value
    {
        get => (string) _dropDown.Button.SelectedMetadata!;
        set => _dropDown.Button.SelectId(FindValueId(value));
    }

    public OptionLanguageCVar(
        OptionsTabControlRow controller,
        IConfigurationManager cfg,
        ILocalizationManager loc,
        CVarDef<string> cVar,
        OptionDropDown dropDown,
        IReadOnlyList<(string cultureName, string label)> options)
        : base(controller, cfg, cVar)
    {
        _loc = loc;
        _dropDown = dropDown;
        _entries = new ItemEntry[options.Count];

        var button = dropDown.Button;
        for (var i = 0; i < options.Count; i++)
        {
            var (cultureName, label) = options[i];
            _entries[i] = new ItemEntry { Key = cultureName };
            button.AddItem(label, i);
            button.SetItemMetadata(button.GetIdx(i), cultureName);
        }

        dropDown.Button.OnItemSelected += args =>
        {
            dropDown.Button.SelectId(args.Id);
            ValueChanged();
        };
    }



    private int FindValueId(string value)
    {
        for (var i = 0; i < _entries.Length; i++)
        {
            if (string.Equals(_entries[i].Key, value, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return 0;
    }

    private struct ItemEntry
    {
        public string Key;
    }
}

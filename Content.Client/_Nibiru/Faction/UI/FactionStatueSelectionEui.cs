using Content.Client.Eui;
using Content.Shared._Nibiru.Factions;
using Content.Shared.Eui;
using JetBrains.Annotations;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using System.Numerics;

namespace Content.Client._Nibiru.Faction.UI;

/// <summary>
/// Client-side EUI for selecting a faction member when building a statue.
/// Shows a list of all faction members and sends the selection to the server.
/// </summary>
/// DONT WORK
[UsedImplicitly]
public sealed class FactionStatueSelectionEui : BaseEui
{
    private FactionStatueSelectionWindow? _window;

    public override void HandleState(EuiStateBase state)
    {
        if (state is not FactionStatueSelectionState selectionState)
            return;

        if (_window == null)
        {
            _window = new FactionStatueSelectionWindow();
            _window.OnMemberSelected += (netEntity) =>
            {
                SendMessage(new FactionStatueSelectMessage { SelectedMember = netEntity });
                _window?.Close();
            };
            _window.OnClose += () => Closed();
        }

        _window.Title = Loc.GetString("faction-statue-select-title");
        _window.Populate(selectionState);
        _window.OpenCentered();
    }

    public override void Opened()
    {
        // ok
    }

    public override void Closed()
    {
        _window?.Close();
        _window = null;
    }
}

/// <summary>
/// Window with a list of faction members to select from.
/// </summary>
public sealed class FactionStatueSelectionWindow : DefaultWindow
{
    private readonly ItemList _memberList;
    private FactionStatueSelectionState? _currentState;

    public event Action<NetEntity>? OnMemberSelected;

    public FactionStatueSelectionWindow()
    {
        MinSize = new Vector2(300, 400);
        _memberList = new ItemList { SelectMode = ItemList.ItemListSelectMode.Button };
        _memberList.OnItemSelected += OnItemSelected;

        var promptLabel = new Label
        {
            Text = Loc.GetString("faction-statue-select-prompt"),
            Margin = new Thickness(5)
        };

        var vBox = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Children =
            {
                promptLabel,
                _memberList
            }
        };

        Contents.AddChild(vBox);
    }

    public void Populate(FactionStatueSelectionState state)
    {
        _currentState = state;
        _memberList.Clear();

        foreach (var member in state.AllMembers)
        {
            _memberList.AddItem(member.Name);
        }
    }

    private void OnItemSelected(ItemList.ItemListSelectedEventArgs args)
    {
        if (args.ItemIndex < 0 || _currentState == null || args.ItemIndex >= _currentState.AllMembers.Count)
            return;

        var member = _currentState.AllMembers[args.ItemIndex];
        OnMemberSelected?.Invoke(member.Entity);
    }
}

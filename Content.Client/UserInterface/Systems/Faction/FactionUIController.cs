using Content.Client.Gameplay;
using Content.Client.UserInterface.Controls;
using JetBrains.Annotations;
using Content.Client.UserInterface.Systems.Faction.UI;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Input.Binding;
using Robust.Shared.Utility;
using Content.Shared._Nibiru.Factions;
using Content.Client.Nibiru.Faction;
using static Robust.Client.UserInterface.Controls.BaseButton;
using Robust.Client.Player;
using Robust.Client.Input;
using Robust.Shared.Input;
using static Robust.Shared.Input.Binding.PointerInputCmdHandler;
using Robust.Shared.Timing;
using Robust.Client.UserInterface.Controls;
using Content.Shared.IdentityManagement;
using System.Xml.Linq;
using Content.Client.Backmen.Research.UI;
using Microsoft.CodeAnalysis.Elfie.Serialization;
using Content.Client.Nibiru.Faction.UI;

namespace Content.Client.UserInterface.Systems.Faction;

[UsedImplicitly]
public sealed class FactionUIController : UIController, IOnStateEntered<GameplayState>, IOnStateExited<GameplayState>
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;

    private FactionMenu? _factionWindow;

    private MenuButton? FactionButton => UIManager.GetActiveUIWidgetOrNull<MenuBar.Widgets.GameTopMenuBar>()?.FactionButton;

    public string _factionName = string.Empty;

    //public override void Initialize()
    //{
    //    base.Initialize();

    //    CommandBinds.Builder
    //            .Bind(EngineKeyFunctions.Use, new PointerInputCmdHandler(OnUse, true, true))
    //            .Register<FactionUIController>();
    //}

    public void UnloadButton()
    {
        if (FactionButton == null)
        {
            return;
        }

        FactionButton.Pressed = false;
        FactionButton.OnPressed -= FactionButtonOnOnPressed;
    }

    public void LoadButton()
    {
        if (FactionButton == null)
        {
            return;
        }

        FactionButton.OnPressed += FactionButtonOnOnPressed;
    }

    private void ActivateButton() => FactionButton!.SetClickPressed(true);
    private void DeactivateButton() => FactionButton!.SetClickPressed(false);

    private void ChangeStateButton(Button button)
    {
        button!.SetClickPressed(!button.Pressed);

        DeactivateStateButton(button);
    }
    private void DeactivateStateButton(Button? button = null)
    {
        if (_factionWindow == null)
            return;

        if (_factionWindow.TransferTitle == button)
            _factionWindow.Heir!.SetClickPressed(false);
        else if (_factionWindow.Heir == button)
            _factionWindow.TransferTitle!.SetClickPressed(false);
        else
        {
            _factionWindow.TransferTitle!.SetClickPressed(false);
            _factionWindow.Heir!.SetClickPressed(false);
            _factionWindow.Delete!.SetClickPressed(false);
            _factionWindow.LeaveButton!.SetClickPressed(false);
        }
    }

    public void OnStateEntered(GameplayState state)
    {
        DebugTools.Assert(_factionWindow == null);

        _factionWindow = UIManager.CreateWindow<FactionMenu>();

        _factionWindow.OnClose += DeactivateButton;
        _factionWindow.OnOpen += ActivateButton;

        _factionWindow.CreateButton.OnPressed += _ =>
        {
            CloseFactionWindow();
			OnCreateFactionButtonPressed();
		};
        _factionWindow.FactionNameChangeButton.OnPressed += _ =>{ OnChangeFactionName();};
        _factionWindow.FactionColorChangeButton.OnPressed += _ => { OnChangeFactionColor();};

        _factionWindow.TransferTitle.OnPressed += _ => ChangeStateButton(_factionWindow.TransferTitle);
        _factionWindow.Heir.OnPressed += _ => ChangeStateButton(_factionWindow.Heir);
        _factionWindow.Delete.OnPressed += _ => { OnDeleteButtonPressed();};
        _factionWindow.LeaveButton.OnPressed += _ => { OnLeaveButtonPressed();};

        DeactivateStateButton();

        CommandBinds.Builder
                .Bind(EngineKeyFunctions.Use, new PointerInputCmdHandler(OnUse, true, true))
                .Register<FactionUIController>();
    }

    public void OnStateExited(GameplayState state)
    {
        if (_factionWindow != null)
        {
            _factionWindow.Dispose();
            _factionWindow = null;
        }

        CommandBinds.Unregister<FactionUIController>();
    }

    private void FactionButtonOnOnPressed(ButtonEventArgs obj)
    {
        ToggleWindow();
    }

    private void CloseFactionWindow()
    {
        _factionWindow?.Close();

        if (_factionWindow == null)
            return;

        DeactivateStateButton();
    }

    /// <summary>
    /// Toggles the game menu.
    /// </summary>
    private void ToggleWindow()
    {
        if (_factionWindow == null)
            return;

        UpdateState();

        if (_factionWindow.IsOpen)
        {
            CloseFactionWindow();
            FactionButton!.Pressed = false;
        }
        else
        {
            _factionWindow.Open();
            FactionButton!.Pressed = true;
        }
    }

    private void OnCreateFactionButtonPressed()
    {
        _factionName = _factionWindow!.LabelLineEdit.Text;

        if (!string.IsNullOrWhiteSpace(_factionName))
        {
            _entityManager.RaisePredictiveEvent(new FactionCreateRequestMessage
            {
                FactionName = _factionName
            });

            UpdateState();
        }
        else
            return;
    }

    private void OnDeleteButtonPressed()
    {
        if (_factionWindow == null || _player.LocalSession is not { } session)
            return;

        var playerEntity = session.AttachedEntity;

        if (!_entityManager.TryGetComponent<FactionComponent>(playerEntity, out var factionComponent))
            return;

        _entityManager.RaisePredictiveEvent(new FactionDeleteMessage());

        CloseFactionWindow();
    }

    private void OnLeaveButtonPressed()
    {
        if (_factionWindow == null || _player.LocalSession is not { } session)
            return;

        var playerEntity = session.AttachedEntity;

        if (!_entityManager.TryGetComponent<FactionComponent>(playerEntity, out var factionComponent))
            return;

        _entityManager.RaisePredictiveEvent(new FactionLeaveMessage());

        CloseFactionWindow();
    }

    private void OnChangeFactionName()
    {
        if (_factionWindow == null)
            return;

        var newName = _factionWindow.FactionNameChange.Text;
        if (!string.IsNullOrWhiteSpace(newName))
        {
            _entityManager.RaisePredictiveEvent(new FactionChangeStateMessage
            {
                FactionName = newName
            });
            UpdateState();
        }
    }

    private void OnChangeFactionColor()
    {
        if (_factionWindow == null || string.IsNullOrEmpty(_factionWindow.FactionColorChange.Text))
            return;

        var color = Color.TryFromHex(_factionWindow.FactionColorChange.Text);
        if (color != null)
        {
            _entityManager.RaisePredictiveEvent(new FactionChangeStateMessage
            {
                Color = color
            });
        }

        if (Color.TryFromName(_factionWindow.FactionColorChange.Text, out var newColor))
        {
            _entityManager.RaisePredictiveEvent(new FactionChangeStateMessage
            {
                Color = newColor
            });
        }

        UpdateState();
    }

    private void UpdateState()
    {
        if (_factionWindow == null || _player.LocalSession is not { } session)
            return;

        var playerEntity = session.AttachedEntity;

        if (!_entityManager.TryGetComponent<FactionComponent>(playerEntity, out var factionComponent))
        {
            _factionWindow.FactionCreate.Visible = true;
            _factionWindow.FactionLeaderWindow.Visible = false;
            _factionWindow.FactionMemberWindow.Visible = false;

            return;
        }
        else if (!factionComponent.IsCreator)
        {
            _factionWindow.FactionCreate.Visible = false;
            _factionWindow.FactionLeaderWindow.Visible = false;
            _factionWindow.FactionMemberWindow.Visible = !_factionWindow.FactionCreate.Visible;

            _factionWindow.FactionLeader.Text = Loc.GetString("faction-leader", ("leaderName", Identity.Name(factionComponent.Leader, _entityManager)));
            _factionWindow.FactionNameMember.SetMessage(Loc.GetString("faction-name-label", ("name", factionComponent.FactionName)), defaultColor: factionComponent.FactionColor);
        }
        else if (factionComponent.IsCreator)
        {
            _factionWindow.FactionCreate.Visible = false;
            _factionWindow.FactionMemberWindow.Visible = false;
            _factionWindow.FactionLeaderWindow.Visible = !_factionWindow.FactionCreate.Visible;

            _factionWindow.FactionName.SetMessage(Loc.GetString("faction-name-label", ("name", factionComponent.FactionName)), defaultColor: factionComponent.FactionColor);

            _factionWindow.MemberContainer.RemoveAllChildren();
            foreach (var member in factionComponent.Members)
            {
                _factionWindow.MemberContainer.AddChild(new MiniMemberCardControl(member, playerEntity, _entityManager));
            }
        }
    }

    private bool OnUse(in PointerInputCmdArgs args)
    {
        if (!_gameTiming.IsFirstTimePredicted)
            return false;

        if (args.State == BoundKeyState.Down)
            return OnMouseDown(args);

        return false;
    }

    private bool OnMouseDown(in PointerInputCmdArgs args)
    {
        // Return if no player entity
        if (_player.LocalEntity is not { } playerEntity || _factionWindow == null)
            return false;

        var entity = args.EntityUid;

        // Return if can not see table or stunned/no hands
        //if (!CanSeeTable(playerEntity, _table) || !CanDrag(playerEntity, entity, out _))
        //{
        //    return false;
        //}

        if (_factionWindow.Heir!.Pressed)
        {
            _entityManager.RaisePredictiveEvent(new HeirChooseMessage
            {
                Heir = _entityManager.GetNetEntity(entity)
            });
            _factionWindow.Heir!.Pressed = false;

            return true;
        }

        if (_factionWindow.TransferTitle!.Pressed)
        {
            _entityManager.RaisePredictiveEvent(new FactionTitleTransferMessage
            {
                entity = _entityManager.GetNetEntity(entity)
            });
            _factionWindow.TransferTitle!.Pressed = false;

            return true;
        }

        UpdateState();
        return false;
    }
}


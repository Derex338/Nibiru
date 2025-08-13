using Content.Client.Gameplay;
using Robust.Shared.Serialization;
using Robust.Client.GameObjects;
using Content.Client.UserInterface.Controls;
using Robust.Shared.IoC;
using Robust.Shared.Network;
using Content.Shared.CCVar;
using JetBrains.Annotations;
using Content.Client.UserInterface.Systems.Faction.UI;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Configuration;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.Utility;
using Content.Shared._Nibiru.Factions;
using Content.Client.UserInterface.Systems.Faction;
using static Robust.Client.UserInterface.Controls.BaseButton;

namespace Content.Client.UserInterface.Systems.Faction;

[UsedImplicitly]
public sealed class FactionUIController : UIController, IOnStateEntered<GameplayState>, IOnStateExited<GameplayState>
{	
	[Dependency] private readonly IEntityManager _entityManager = default!;

    private FactionMenu? _factionWindow;

    private MenuButton? FactionButton => UIManager.GetActiveUIWidgetOrNull<MenuBar.Widgets.GameTopMenuBar>()?.FactionButton;
	
	public string _factionName = string.Empty;

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

        CommandBinds.Builder.Register<FactionUIController>();
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
    }

    /// <summary>
    /// Toggles the game menu.
    /// </summary>
    private void ToggleWindow()
    {
        if (_factionWindow == null)
            return;

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
        // Получаем текст из текстового поля
        _factionName = _factionWindow!.LabelLineEdit.Text;
	
        //Проверяем, что имя фракции не пустое
        if (!string.IsNullOrWhiteSpace(_factionName))
        {
            _entityManager.RaisePredictiveEvent(new FactionCreateRequestMessage
			{
				FactionName = _factionName
			});
        }
		else
		{
            return;
		}
    }
}


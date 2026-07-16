using Content.Client._Nibiru.Construction;
using Content.Client.Construction.UI;
using Content.Client.Gameplay;
using Content.Client.Localization;
using Content.Client.UserInterface.Controls;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;
using static Content.Client._Nibiru.Construction.ConstructionRecipeCheck;

namespace Content.Client.UserInterface.Systems.Crafting;

[UsedImplicitly]
public sealed class CraftingUIController : UIController, IOnStateChanged<GameplayState>, IOnSystemChanged<ConstructionRecipeCheck>, ILanguageRefreshable
{
    private ConstructionMenuPresenter? _presenter;
    private MenuButton? CraftingButton => UIManager.GetActiveUIWidgetOrNull<MenuBar.Widgets.GameTopMenuBar>()?.CraftingButton;

    [UISystemDependency] private readonly ConstructionRecipeCheck _recipeCheck = default!;

    public void OnStateEntered(GameplayState state)
    {
        DebugTools.Assert(_presenter == null);
        LanguageRefreshManager.Register(this);
        _presenter = new ConstructionMenuPresenter();
        _recipeCheck.RequestRecipeInfo();
    }

    public void OnStateExited(GameplayState state)
    {
        LanguageRefreshManager.Unregister(this);
        if (_presenter == null)
            return;
        UnloadButton(_presenter);
        _presenter.Dispose();
        _presenter = null;
    }

    public void OnLanguageChanged()
    {
        if (_presenter == null || !_presenter.WindowOpen)
            return;

        _presenter.WindowOpen = false;
        _presenter.WindowOpen = true;
    }

    internal void UnloadButton(ConstructionMenuPresenter? presenter = null)
    {
        if (CraftingButton == null)
        {
            return;
        }

        if (presenter == null)
        {
            presenter ??= _presenter;
            if (presenter == null)
            {
                return;
            }
        }

        CraftingButton.Pressed = false;
        CraftingButton.OnToggled -= presenter.OnHudCraftingButtonToggled;
    }

    public void LoadButton()
    {
        if (CraftingButton == null)
        {
            return;
        }

        CraftingButton.OnToggled += ButtonToggled;
        CraftingButton.OnToggled += RequestRecipeInfo;
    }

    private void ButtonToggled(BaseButton.ButtonToggledEventArgs obj)
    {
        _presenter?.OnHudCraftingButtonToggled(obj);
    }

    public void OnSystemLoaded(ConstructionRecipeCheck system)
    {
        system.OnConstructionRecipeUpdate += RecipeUpdated;
    }

    public void OnSystemUnloaded(ConstructionRecipeCheck system)
    {
        system.OnConstructionRecipeUpdate -= RecipeUpdated;
    }

    private void RecipeUpdated(RecipeData data)
    {
        _presenter?.OnConstructionCrafts(data.crafts, this);
    }

    private void RequestRecipeInfo(BaseButton.ButtonToggledEventArgs obj)
    {
        _recipeCheck.RequestRecipeInfo();
    }
}

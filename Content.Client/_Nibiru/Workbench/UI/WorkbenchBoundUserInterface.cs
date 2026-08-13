using Content.Shared._Nibiru.Workbench;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Nibiru.Workbench.UI;

public sealed class WorkbenchBoundUserInterface : BoundUserInterface
{
    private WorkbenchWindow? _menu;

    public WorkbenchBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindow<WorkbenchWindow>();
        _menu.SetEntity(Owner);
        SendMessage(new RequestRecipesWorkbenchMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        switch (state)
        {
            case WorkbenchUpdateState msg:
                if (_menu != null)
                _menu.ConsRecipes = msg.Recipes;
                _menu?.OnViewPopulateRecipes(this, (string.Empty, string.Empty));
                //_menu.PopulateInfo(_selected);
                //UpdateGhostPlacement();
                break;
        }
    }
}


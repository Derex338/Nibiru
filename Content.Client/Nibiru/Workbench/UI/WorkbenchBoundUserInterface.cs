using Content.Shared._Nibiru.Workbench;
using Content.Shared.Lathe;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client.Nibiru.Workbench.UI
{
    [UsedImplicitly]
    public sealed class WorkbenchBoundUserInterface : BoundUserInterface
    {
        [ViewVariables]
        private WorkbenchWindow? _menu;
        public WorkbenchBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
        {
        }

        protected override void Open()
        {
            base.Open();

            _menu = this.CreateWindowCenteredRight<WorkbenchWindow>();
            _menu.SetEntity(Owner);

            _menu.PopulateRecipes += (_, _) =>
            {
                SendMessage(new RequestRecipesWorkbenchMessage());
            };

            //_menu.OnServerListButtonPressed += _ =>
            //{
            //    SendMessage(new ConsoleServerSelectionMessage());
            //};

            //_menu.RecipeQueueAction += (recipe, amount) =>
            //{
            //    SendMessage(new LatheQueueRecipeMessage(recipe, amount));
            //};
            //_menu.QueueDeleteAction += index => SendMessage(new LatheDeleteRequestMessage(index));
            //_menu.QueueMoveUpAction += index => SendMessage(new LatheMoveRequestMessage(index, -1));
            //_menu.QueueMoveDownAction += index => SendMessage(new LatheMoveRequestMessage(index, 1));
            //_menu.DeleteFabricatingAction += () => SendMessage(new LatheAbortFabricationMessage());
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
}

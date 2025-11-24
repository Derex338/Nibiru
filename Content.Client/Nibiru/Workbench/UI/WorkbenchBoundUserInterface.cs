using Content.Client.Construction;
using Content.Client.Construction.UI;
using Content.Client.Nibiru.Construction;
using Content.Client.Stylesheets;
using Content.Shared.Construction.Prototypes;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Placement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using System.Numerics;

namespace Content.Client.Nibiru.Workbench.UI
{
    [UsedImplicitly]
    public sealed class WorkbenchBoundUserInterface : BoundUserInterface
    {
        [Dependency] private readonly IPlacementManager _placementManager = default!;
        [Dependency] private readonly IEntitySystemManager _systemManager = default!;
        [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
        [Dependency] private readonly EntityManager _entManager = default!;
        private readonly SpriteSystem _spriteSystem;

        [UISystemDependency] private readonly ConstructionRecipeCheck _recipeCheck = default!;

        private ConstructionPrototype? _selected;
        private ConstructionSystem? _constructionSystem;
        private string _selectedCategory = string.Empty;
        public List<ProtoId<ConstructionPrototype>> ConsRecipes = new();
        private Dictionary<string, ContainerButton> _recipeButtons = new();

        private List<ConstructionPrototype> _favoritedRecipes = [];

        private const string FavoriteCatName = "construction-category-favorites";
        private const string ForAllCategoryName = "construction-category-all";

        [ViewVariables]
        private WorkbenchWindow? _menu;
        public WorkbenchBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
        {
            _spriteSystem = _entManager.System<SpriteSystem>();
        }

        protected override void Open()
        {
            base.Open();

            _menu = this.CreateWindowCenteredRight<WorkbenchWindow>();
            _menu.SetEntity(Owner);

            _menu.PopulateRecipes += OnViewPopulateRecipes;
            _recipeCheck?.RequestRecipeInfo();
            if (_recipeCheck is not null)
                _recipeCheck.OnConstructionRecipeUpdate += (data) => { ConsRecipes = data.crafts; OnViewPopulateRecipes(_menu, (string.Empty, string.Empty)); };

            if (_systemManager.TryGetEntitySystem<ConstructionSystem>(out var constructionSystem))
                _constructionSystem = constructionSystem;

            _menu.RecipeSelected += (sender, item) => {
                if (item is null)
                {
                    _selected = null;
                    _menu.ClearRecipeInfo();
                    return;
                }

                _selected = item.Prototype;

                //if (_placementManager is { IsActive: true, Eraser: false })
                    //UpdateGhostPlacement();

                //PopulateInfo(_selected);
            };

            _menu.BuildButtonToggled += (_, b) =>
            {
                if (_selected == null || !b || _constructionSystem == null)
                    return;

                _placementManager.BeginPlacing(new PlacementInformation
                {
                    IsTile = false,
                    PlacementOption = _selected.PlacementMode
                },
                    new ConstructionPlacementHijack(_constructionSystem, _selected));
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

            //switch (state)
            //{
            //    case LatheUpdateState msg:
            //        if (_menu != null)
            //            _menu.Recipes = msg.Recipes;
            //        _menu?.PopulateRecipes();
            //        _menu?.UpdateCategories();
            //        _menu?.PopulateQueueList(msg.Queue);
            //        _menu?.SetQueueInfo(msg.CurrentlyProducing);
            //        break;
            //}
        }

        public void OnViewPopulateRecipes(object? sender, (string search, string catagory) args)
        {
            if (_constructionSystem is null || _menu is null)
                return;

            var actualRecipes = GetAndSortRecipes(args);

            var recipesList = _menu.Recipes;
            var recipesGrid = _menu.RecipesGrid;
            recipesGrid.RemoveAllChildren();

            _menu.RecipesGridScrollContainer.Visible = _menu.GridViewButtonPressed;
            _menu.Recipes.Visible = !_menu.GridViewButtonPressed;

            if (_menu.GridViewButtonPressed)
            {
                recipesList.PopulateList([]);
                PopulateGrid(recipesGrid, actualRecipes);
            }
            else
            {
                recipesList.PopulateList(actualRecipes);
            }
        }

        private List<ConstructionMenu.ConstructionMenuListData> GetAndSortRecipes((string, string) args)
        {
            var recipes = new List<ConstructionMenu.ConstructionMenuListData>();

            var (search, category) = args;
            var isEmptyCategory = string.IsNullOrEmpty(category) || category == ForAllCategoryName;
            _selectedCategory = isEmptyCategory ? string.Empty : category;

            //_constructionSystem?.OpenUI();

            //var Netentity = _playerManager.LocalEntity;
            //if (Netentity != null)
            //_sharedConstruction.OpenUI();
            //RaiseNetworkEvent(new ConstructionUIOpen(GetNetEntity(entity.Value)));

            //ConsRecipes.Add(new("MimeHardsuit"));

            foreach (var id in ConsRecipes)//_prototypeManager.EnumeratePrototypes<ConstructionPrototype>())
            {
                if (!_prototypeManager.TryIndex(id, out var recipe))
                    continue;
                /*
                if (recipe.Hide)
                    continue;

                if (_playerManager.LocalSession == null
                    || _playerManager.LocalEntity == null
                    || _whitelistSystem.IsWhitelistFail(recipe.EntityWhitelist, _playerManager.LocalEntity.Value))
                    continue;

                if (!string.IsNullOrEmpty(search) && (recipe.Name is { } name &&
                                                      !name.Contains(search.Trim(),
                                                          StringComparison.InvariantCultureIgnoreCase)))
                    continue;

                if (!isEmptyCategory)
                {
                    if ((category != FavoriteCatName || !_favoritedRecipes.Contains(recipe)) &&
                        recipe.Category != category)
                        continue;
                }
*/
                if (!_constructionSystem!.TryGetRecipePrototype(recipe.ID, out var targetProtoId))
                {
                    Logger.Error("Cannot find the target prototype in the recipe cache with the id \"{0}\" of {1}.",
                        recipe.ID,
                        nameof(ConstructionPrototype));
                    continue;
                }

                if (!_prototypeManager.TryIndex(targetProtoId, out EntityPrototype? proto))
                    continue;

                recipes.Add(new(recipe, proto));
            }

            recipes.Sort(
                (a, b) => string.Compare(a.Prototype.Name, b.Prototype.Name, StringComparison.InvariantCulture));

            return recipes;
        }

        private void PopulateGrid(GridContainer recipesGrid,
            IEnumerable<ConstructionMenu.ConstructionMenuListData> actualRecipes)
        {
            foreach (var recipe in actualRecipes)
            {
                var protoView = new EntityPrototypeView()
                {
                    Scale = new Vector2(1.2f),
                };
                protoView.SetPrototype(recipe.TargetPrototype);

                var itemButton = new ContainerButton()
                {
                    VerticalAlignment = Control.VAlignment.Center,
                    Name = recipe.TargetPrototype.Name,
                    ToolTip = recipe.TargetPrototype.Name,
                    ToggleMode = true,
                    Children = { protoView },
                };

                var itemButtonPanelContainer = new PanelContainer
                {
                    PanelOverride = new StyleBoxFlat { BackgroundColor = StyleNano.ButtonColorDefault },
                    Children = { itemButton },
                };

                itemButton.OnToggled += buttonToggledEventArgs =>
                {
                    SelectGridButton(itemButton, buttonToggledEventArgs.Pressed);

                    if (buttonToggledEventArgs.Pressed &&
                        _selected != null &&
                        _recipeButtons.TryGetValue(_selected.Name!, out var oldButton))
                    {
                        oldButton.Pressed = false;
                        SelectGridButton(oldButton, false);
                    }

                    OnGridViewRecipeSelected(this, buttonToggledEventArgs.Pressed ? recipe.Prototype : null);
                };

                recipesGrid.AddChild(itemButtonPanelContainer);
                _recipeButtons[recipe.Prototype.Name!] = itemButton;
                var isCurrentButtonSelected = _selected == recipe.Prototype;
                itemButton.Pressed = isCurrentButtonSelected;
                SelectGridButton(itemButton, isCurrentButtonSelected);
            }
        }

        private void SelectGridButton(BaseButton button, bool select)
        {
            if (button.Parent is not PanelContainer buttonPanel)
                return;

            button.Modulate = select ? Color.Green : Color.Transparent;
            var buttonColor = select ? StyleNano.ButtonColorDefault : Color.Transparent;
            buttonPanel.PanelOverride = new StyleBoxFlat { BackgroundColor = buttonColor };
        }

        private void OnGridViewRecipeSelected(object? _, ConstructionPrototype? recipe)
        {
            if (recipe is null && _menu is not null)
            {
                _selected = null;
                _menu.ClearRecipeInfo();
                return;
            }

            _selected = recipe;

            if (_placementManager is { IsActive: true, Eraser: false })
                UpdateGhostPlacement();

            PopulateInfo(_selected);
        }

        private void UpdateGhostPlacement()
        {
            if (_selected == null || _menu == null)
                return;

            if (_selected.Type != ConstructionType.Structure)
            {
                _placementManager.Clear();
                return;
            }

            var constructSystem = _systemManager.GetEntitySystem<ConstructionSystem>();

            _placementManager.BeginPlacing(new PlacementInformation()
            {
                IsTile = false,
                PlacementOption = _selected.PlacementMode,
            },
                new ConstructionPlacementHijack(constructSystem, _selected));

            _menu.BuildButtonPressed = true;
        }

        private void PopulateInfo(ConstructionPrototype? prototype)
        {
            if (_constructionSystem is null || _menu is null)
                return;

            _menu.ClearRecipeInfo();

            if (prototype is null)
                return;

            if (!_constructionSystem.TryGetRecipePrototype(prototype.ID, out var targetProtoId))
                return;

            if (!_prototypeManager.TryIndex(targetProtoId, out EntityPrototype? proto))
                return;

            _menu.SetRecipeInfo(
                prototype.Name!,
                prototype.Description!,
                proto,
                prototype.Type != ConstructionType.Item,
                !_favoritedRecipes.Contains(prototype));

            var stepList = _menu.RecipeStepList;
            GenerateStepList(prototype, stepList);
        }

        private void GenerateStepList(ConstructionPrototype prototype, ItemList stepList)
        {
            if (_constructionSystem?.GetGuide(prototype) is not { } guide)
                return;

            foreach (var entry in guide.Entries)
            {
                var text = entry.Arguments != null
                    ? Loc.GetString(entry.Localization, entry.Arguments)
                    : Loc.GetString(entry.Localization);

                if (entry.EntryNumber is { } number)
                {
                    text = Loc.GetString("construction-presenter-step-wrapper",
                        ("step-number", number),
                        ("text", text));
                }

                // The padding needs to be applied regardless of text length... (See PadLeft documentation)
                text = text.PadLeft(text.Length + entry.Padding);

                var icon = entry.Icon != null ? _spriteSystem.Frame0(entry.Icon) : Texture.Transparent;
                stepList.AddItem(text, icon, false);
            }
        }
    }
}

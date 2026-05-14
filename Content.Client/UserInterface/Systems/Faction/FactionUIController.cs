using Content.Client.Gameplay;
using Content.Client.UserInterface.Controls;
using JetBrains.Annotations;
using Content.Client.UserInterface.Systems.Faction.UI;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Input.Binding;
using Robust.Shared.Utility;
using Robust.Client.UserInterface.CustomControls;
using Content.Shared._Nibiru.Factions;
using Content.Shared._Nibiru.Factions.Messeges;
using Content.Client.Nibiru.Faction;
using Content.Client._Nibiru.Factions;
using static Robust.Client.UserInterface.Controls.BaseButton;
using Robust.Client.Player;
using Robust.Client.Input;
using Robust.Shared.Input;
using static Robust.Shared.Input.Binding.PointerInputCmdHandler;
using Robust.Shared.Timing;
using Robust.Client.UserInterface.Controls;
using Content.Shared.IdentityManagement;
using Content.Client.Nibiru.Faction.UI;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;
using System.Linq;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.Maths;
using Robust.Client.UserInterface;
using System.Numerics;
using Content.Shared.StatusIcon;

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
        {
            _factionWindow.Heir!.SetClickPressed(false);
        }
        else if (_factionWindow.Heir == button)
        {
            _factionWindow.TransferTitle!.SetClickPressed(false);
        }
        else
        {
            _factionWindow.TransferTitle!.SetClickPressed(false);
            _factionWindow.Heir!.SetClickPressed(false);
            _factionWindow.Delete!.SetClickPressed(false);
            _factionWindow.LeaveButton!.SetClickPressed(false);
            ToggleMemberOutlines(false);
        }
    }

    public void OnStateEntered(GameplayState state)
    {
        DebugTools.Assert(_factionWindow == null);

        _factionWindow = UIManager.CreateWindow<FactionMenu>();

        _factionWindow.OnClose += () =>
        {
            DeactivateButton();
            ToggleMemberOutlines(false);
        };
        _factionWindow.OnOpen += ActivateButton;

        _factionWindow.CreateButton.OnPressed += _ =>
        {
            CloseFactionWindow();
			OnCreateFactionButtonPressed();
		};
        _factionWindow.FactionNameChangeButton.OnPressed += _ =>{ OnChangeFactionName();};
        _factionWindow.FactionColorChangeButton.OnPressed += _ => { OnChangeFactionColor();};

        _factionWindow.TransferTitle.OnPressed += _ =>
        {
            ChangeStateButton(_factionWindow.TransferTitle);
            ToggleMemberOutlines(_factionWindow.TransferTitle.Pressed);
        };
        _factionWindow.Heir.OnPressed += _ =>
        {
            ChangeStateButton(_factionWindow.Heir);
            ToggleMemberOutlines(_factionWindow.Heir.Pressed);
        };
        _factionWindow.Delete.OnPressed += _ => { OnDeleteButtonPressed();};
        _factionWindow.LeaveButton.OnPressed += _ => { OnLeaveButtonPressed();};

        _factionWindow.CreateRoleButton.OnPressed += _ =>
        {
            if (_player.LocalSession is not { } session)
                return;
            var playerEntity = session.AttachedEntity;
            if (!_entityManager.TryGetComponent<FactionComponent>(playerEntity, out var fc))
                return;
            var prompt = new RoleManagePrompt(fc.Roles, _entityManager);
            prompt.OpenCentered();
        };

        _factionWindow.FactionDescriptionChangeButton.OnPressed += _ => { OnChangeDescription(); };
        _factionWindow.FactionIconChangeButton.OnPressed += _ => { OnChangeIcon(); };
        _factionWindow.RecruitingToggle.OnPressed += _ => { OnToggleRecruiting(); };

        _factionWindow.FilterSpeciesButton.OnPressed += _ => OnChangeFilterSpecies();
        _factionWindow.FilterGenderButton.OnPressed += _ => OnChangeFilterGender();
        _factionWindow.FilterNameButton.OnPressed += _ => OnChangeFilterName();

        _factionWindow.EditLogoButton.OnPressed += _ => OnEditLogo();

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
            _factionWindow.OpenCentered();
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

            _factionWindow.FactionLeader.Text = Identity.Name(factionComponent.Leader, _entityManager);
            _factionWindow.FactionNameMember.SetMessage(Loc.GetString("faction-name-label", ("name", factionComponent.FactionName)), defaultColor: factionComponent.FactionColor);

            var rank = string.IsNullOrEmpty(factionComponent.Rank) ? Loc.GetString("faction-rank-no-rank") : factionComponent.Rank;
            _factionWindow.FactionRank.Text = Loc.GetString("faction-rank-label", ("rank", rank));

            if (!string.IsNullOrEmpty(factionComponent.IconPath))
            {
                var spriteSystem = _entityManager.System<SpriteSystem>();
                var protoManager = IoCManager.Resolve<IPrototypeManager>();

                if (protoManager.TryIndex<StatusIconPrototype>(factionComponent.IconPath, out var iconProto))
                {
                    _factionWindow.FactionIconMember.Texture = spriteSystem.Frame0(iconProto.Icon);
                }
                else
                {
                    var resPath = new ResPath(factionComponent.IconPath);
                    _factionWindow.FactionIconMember.Texture = spriteSystem.Frame0(new SpriteSpecifier.Texture(resPath));
                }
            }

            _factionWindow.FactionLogoMember.UpdateLogo(factionComponent.LogoBackground, factionComponent.LogoPixels);
        }
        else if (factionComponent.IsCreator)
        {
            _factionWindow.FactionCreate.Visible = false;
            _factionWindow.FactionMemberWindow.Visible = false;
            _factionWindow.FactionLeaderWindow.Visible = !_factionWindow.FactionCreate.Visible;

            _factionWindow.FactionName.SetMessage(Loc.GetString("faction-name-label", ("name", factionComponent.FactionName)), defaultColor: factionComponent.FactionColor);

            _factionWindow.FactionNameChange.Text = factionComponent.FactionName;
            _factionWindow.FactionDescriptionChange.Text = factionComponent.Description;
            _factionWindow.FactionColorChange.Text = factionComponent.FactionColor.ToHex();
            _factionWindow.FactionIconChange.Text = factionComponent.IconPath;
            _factionWindow.RecruitingToggle.Pressed = factionComponent.IsRecruiting;

            _factionWindow.FilterSpeciesLabel.Text = factionComponent.WhiteListSpecies.Count == 0 ? "Все" : string.Join(", ", factionComponent.WhiteListSpecies);
            _factionWindow.FilterGenderLabel.Text = factionComponent.WhiteListGender.Count == 0 ? "Все" : string.Join(", ", factionComponent.WhiteListGender);
            
            UpdateSkinColorFiltersUI(factionComponent);
            
            _factionWindow.FilterName.Text = string.Join(", ", factionComponent.WhiteListNames);

            _factionWindow.MemberContainer.RemoveAllChildren();
            foreach (var memberData in factionComponent.MemberData)
            {
                _factionWindow.MemberContainer.AddChild(new MiniMemberCardControl(memberData, playerEntity, factionComponent.Roles, _entityManager));
            }

            _factionWindow.FactionLogoLeader.UpdateLogo(factionComponent.LogoBackground, factionComponent.LogoPixels);
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

    private void OnChangeDescription()
    {
        if (_factionWindow == null)
            return;

        var newDesc = _factionWindow.FactionDescriptionChange.Text;
        if (!string.IsNullOrWhiteSpace(newDesc))
        {
            _entityManager.RaisePredictiveEvent(new FactionChangeStateMessage
            {
                Description = newDesc
            });
            UpdateState();
        }
    }

    private void OnChangeIcon()
    {
        if (_factionWindow == null)
            return;

        var newIcon = _factionWindow.FactionIconChange.Text;
        if (!string.IsNullOrWhiteSpace(newIcon))
        {
            _entityManager.RaisePredictiveEvent(new FactionChangeStateMessage
            {
                IconPath = newIcon
            });
            UpdateState();
        }
    }

    private void OnToggleRecruiting()
    {
        if (_factionWindow == null || _player.LocalSession is not { } session)
            return;

        var playerEntity = session.AttachedEntity;

        if (!_entityManager.TryGetComponent<FactionComponent>(playerEntity, out var factionComponent))
            return;

        _entityManager.RaisePredictiveEvent(new FactionChangeStateMessage
        {
            IsRecruiting = _factionWindow.RecruitingToggle.Pressed
        });

        UpdateState();
    }

    private void OnChangeFilterSpecies()
    {
        if (_factionWindow == null || _player.LocalSession is not { } session) return;
        if (!_entityManager.TryGetComponent<FactionComponent>(session.AttachedEntity, out var fc)) return;

        var protoManager = IoCManager.Resolve<IPrototypeManager>();
        var species = protoManager.EnumeratePrototypes<SpeciesPrototype>()
            .Where(s => s.RoundStart)
            .Select(s => s.ID)
            .ToList();

        var prompt = new FilterSelectorPrompt("Выбор рас", species, fc.WhiteListSpecies, selected =>
        {
            _entityManager.RaisePredictiveEvent(new FactionChangeStateMessage { WhiteListSpecies = selected });
            UpdateState();
        });
        prompt.OpenCentered();
    }

    private void OnChangeFilterGender()
    {
        if (_factionWindow == null || _player.LocalSession is not { } session) return;
        if (!_entityManager.TryGetComponent<FactionComponent>(session.AttachedEntity, out var fc)) return;

        var genders = Enum.GetValues<Sex>().Select(s => s.ToString()).ToList();
        var current = fc.WhiteListGender.Select(s => s.ToString()).ToList();

        var prompt = new FilterSelectorPrompt("Выбор пола", genders, current, selected =>
        {
            var sexList = new List<Sex>();
            foreach (var s in selected)
            {
                if (Enum.TryParse<Sex>(s, out var sex))
                    sexList.Add(sex);
            }
            _entityManager.RaisePredictiveEvent(new FactionChangeStateMessage { WhiteListGender = sexList });
            UpdateState();
        });
        prompt.OpenCentered();
    }

    private void OnChangeFilterName()
    {
        if (_factionWindow == null) return;
        var text = _factionWindow.FilterName.Text;
        var names = text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        _entityManager.RaisePredictiveEvent(new FactionChangeStateMessage { WhiteListNames = names });
    }

    private void OnEditLogo()
    {
        if (_factionWindow == null || _player.LocalSession is not { } session) return;
        if (!_entityManager.TryGetComponent<FactionComponent>(session.AttachedEntity, out var fc)) return;

        var editor = new FactionLogoEditorWindow();
        editor.LoadLogo(fc.LogoBackground, fc.LogoPixels);
        editor.OnSaveLogo += (bg, pixels) =>
        {
            _entityManager.RaisePredictiveEvent(new NibiruFactionLogoSaveMessage
            {
                BackgroundColor = bg,
                Pixels = pixels
            });

            _entityManager.System<NibiruFactionLogoSystem>().UpdateFactionLogo(fc.FactionName, bg, pixels);
        };
        editor.OpenCentered();
    }

    /// <summary>
    /// Подсветка членов фракции при выборе наследника или передаче титула
    /// </summary>
    private void ToggleMemberOutlines(bool enable)
    {
        if (_player.LocalSession is not { } session)
            return;

        var playerEntity = session.AttachedEntity;
        if (!_entityManager.TryGetComponent<FactionComponent>(playerEntity, out var factionComponent))
            return;

        var protoManager = IoCManager.Resolve<IPrototypeManager>();

        foreach (var member in factionComponent.Members)
        {
            if (member == playerEntity)
                continue;

            if (!_entityManager.TryGetComponent<SpriteComponent>(member, out var sprite))
                continue;

            if (enable)
            {
                var shader = protoManager.Index<ShaderPrototype>("SelectionOutlineInrange").InstanceUnique();
                shader.SetParameter("outline_width", 1f);
                sprite.PostShader = shader;
                sprite.RenderOrder = 1;
            }
            else
            {
                sprite.PostShader = null;
                sprite.RenderOrder = 0;
            }
        }
    }

    private void UpdateSkinColorFiltersUI(FactionComponent factionComponent)
    {
        var container = _factionWindow!.SkinColorFiltersContainer;
        if (container == null) return;
        
        container.RemoveAllChildren();

        if (factionComponent.WhiteListSpecies.Count == 0)
        {
            container.AddChild(new Label { Text = "Выберите расы в фильтре выше", FontColorOverride = Color.Gray });
            return;
        }

        var protoManager = IoCManager.Resolve<IPrototypeManager>();

        foreach (var speciesId in factionComponent.WhiteListSpecies)
        {
            if (!protoManager.TryIndex<SpeciesPrototype>(speciesId, out var speciesProto))
                continue;

            var colorationProto = protoManager.Index<SkinColorationPrototype>(speciesProto.SkinColoration);

            var speciesBox = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, Margin = new Thickness(0, 0, 0, 10) };
            
            bool isEnabled = factionComponent.WhiteListSkinColors.TryGetValue(speciesId, out var currentFilter);

            var topRow = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal };
            var cb = new CheckBox { Text = Loc.GetString(speciesProto.Name), Pressed = isEnabled };
            topRow.AddChild(cb);
            speciesBox.AddChild(topRow);

            var settingsBox = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, Visible = isEnabled, Margin = new Thickness(10, 5, 0, 0) };
            speciesBox.AddChild(settingsBox);

            if (colorationProto.Strategy.InputType == SkinColorationStrategyInput.Unary)
            {
                var slider = new Slider
                {
                    MinValue = 0,
                    MaxValue = 100,
                    HorizontalExpand = true,
                    BackgroundStyleBoxOverride = new ColorSelectorStyleBox(ColorSelectorStyleBox.ColorSliderPreset.Value)
                };
                var styleBox = (ColorSelectorStyleBox) slider.BackgroundStyleBoxOverride;

                if (isEnabled)
                    slider.Value = colorationProto.Strategy.ToUnary(currentFilter.Color);
                else
                    slider.Value = speciesProto.DefaultHumanSkinTone;

                Action updatePreview = () =>
                {
                    var color = colorationProto.Strategy.FromUnary(slider.Value);
                    styleBox.SetBaseColor(color);
                };

                updatePreview();

                var modeBtn = new Button { Text = isEnabled && currentFilter.PassHigher ? "Пропускать темнее или равно" : "Пропускать светлее или равно" };

                Action save = () =>
                {
                    var dict = new Dictionary<string, FactionSkinColorFilter>(factionComponent.WhiteListSkinColors);
                    if (cb.Pressed)
                    {
                        var color = colorationProto.Strategy.FromUnary(slider.Value);
                        dict[speciesId] = new FactionSkinColorFilter { Color = color, PassHigher = modeBtn.Text == "Пропускать темнее или равно" };
                    }
                    else
                    {
                        dict.Remove(speciesId);
                    }
                    _entityManager.RaisePredictiveEvent(new FactionChangeStateMessage { WhiteListSkinColors = dict });
                };

                slider.OnValueChanged += _ =>
                {
                    updatePreview();
                    save();
                };
                modeBtn.OnPressed += _ =>
                {
                    modeBtn.Text = modeBtn.Text == "Пропускать светлее или равно" ? "Пропускать темнее или равно" : "Пропускать светлее или равно";
                    save();
                };
                cb.OnToggled += _ => save();

                settingsBox.AddChild(new Label { Text = "Тон кожи (левее - светлее):" });
                settingsBox.AddChild(slider);
                settingsBox.AddChild(modeBtn);
            }
            else
            {
                var colorSelector = new ColorSelectorSliders { HorizontalExpand = true };
                if (isEnabled)
                    colorSelector.Color = currentFilter.Color;
                else
                    colorSelector.Color = speciesProto.DefaultSkinTone;

                var modeBtn = new Button { Text = isEnabled && currentFilter.PassHigher ? "Пропускать светлее (по HSV)" : "Пропускать темнее (по HSV)" };

                Action save = () =>
                {
                    var dict = new Dictionary<string, FactionSkinColorFilter>(factionComponent.WhiteListSkinColors);
                    if (cb.Pressed)
                    {
                        dict[speciesId] = new FactionSkinColorFilter { Color = colorSelector.Color, PassHigher = modeBtn.Text == "Пропускать светлее (по HSV)" };
                    }
                    else
                    {
                        dict.Remove(speciesId);
                    }
                    _entityManager.RaisePredictiveEvent(new FactionChangeStateMessage { WhiteListSkinColors = dict });
                };

                colorSelector.OnColorChanged += _ => save();
                modeBtn.OnPressed += _ =>
                {
                    modeBtn.Text = modeBtn.Text == "Пропускать светлее (по HSV)" ? "Пропускать темнее (по HSV)" : "Пропускать светлее (по HSV)";
                    save();
                };
                cb.OnToggled += _ => save();

                settingsBox.AddChild(colorSelector);
                settingsBox.AddChild(modeBtn);
            }

            container.AddChild(speciesBox);
        }
    }
}

public sealed class FilterSelectorPrompt : DefaultWindow
{
    private readonly Action<List<string>> _onSave;
    private readonly List<string> _selected;
    private readonly BoxContainer _container;

    public FilterSelectorPrompt(string title, List<string> options, List<string> current, Action<List<string>> onSave)
    {
        Title = title;
        _onSave = onSave;
        _selected = new List<string>(current);

        MinSize = new Vector2(300, 400);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(10)
        };

        var scroll = new ScrollContainer
        {
            VerticalExpand = true,
            HorizontalExpand = true
        };

        _container = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 5
        };

        foreach (var option in options)
        {
            var cb = new CheckBox
            {
                Text = option,
                Pressed = _selected.Contains(option)
            };
            cb.OnToggled += args =>
            {
                if (args.Pressed)
                {
                    if (!_selected.Contains(option))
                        _selected.Add(option);
                }
                else
                {
                    _selected.Remove(option);
                }
            };
            _container.AddChild(cb);
        }

        scroll.AddChild(_container);
        root.AddChild(scroll);

        var saveBtn = new Button
        {
            Text = "Сохранить",
            HorizontalAlignment = HAlignment.Center,
            Margin = new Thickness(0, 10, 0, 0)
        };
        saveBtn.OnPressed += _ =>
        {
            _onSave(_selected);
            Close();
        };
        root.AddChild(saveBtn);

        Contents.AddChild(root);
    }
}


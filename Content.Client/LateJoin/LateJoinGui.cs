using System.Linq;
using System.Numerics;
using Content.Client.GameTicking.Managers;
using Content.Client.UserInterface.Controls;
using Content.Shared._Nibiru.Factions;
using Content.Shared.Humanoid;
using Content.Shared.StatusIcon;
using Robust.Client.Console;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Content.Client.Lobby;
using Content.Shared.Preferences;
using static Robust.Client.UserInterface.Controls.BoxContainer;

namespace Content.Client.LateJoin
{
    /// <summary>
    /// GUI для выбора фракции при позднем присоединении
    /// </summary>
    public sealed class LateJoinGui : DefaultWindow
    {
        [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
        [Dependency] private readonly IEntitySystemManager _entitySystem = default!;
        [Dependency] private readonly ILogManager _logManager = default!;
        [Dependency] private readonly IEntityManager _entityManager = default!;
        [Dependency] private readonly IClientConsoleHost _consoleHost = default!;

        private readonly ClientGameTicker _gameTicker;
        private readonly SpriteSystem _sprites;
        private readonly ISawmill _sawmill;

        private readonly Dictionary<string, FactionButton> _factionButtons = new();
        private readonly BoxContainer _factionList;
        private readonly ScrollContainer _factionScroll;
        private readonly BoxContainer _characterList;
        private readonly ScrollContainer _characterScroll;
        private readonly Button _soloButton;
        private readonly Robust.Client.UserInterface.Controls.RichTextLabel _nibiruDescription;
        [Dependency] private readonly IClientPreferencesManager _prefsManager = default!;

        public LateJoinGui()
        {
            MinSize = SetSize = new Vector2(600, 500);
            IoCManager.InjectDependencies(this);

            _sprites = _entitySystem.GetEntitySystem<SpriteSystem>();
            _gameTicker = _entitySystem.GetEntitySystem<ClientGameTicker>();
            _sawmill = _logManager.GetSawmill("faction.latejoin");

            Title = Loc.GetString("late-join-gui-title");

            var baseContainer = new BoxContainer()
            {
                Orientation = LayoutOrientation.Vertical,
                VerticalExpand = true,
            };

            // Заголовок
            baseContainer.AddChild(new StripeBack
            {
                Children =
                {
                    new PanelContainer
                    {
                        Children =
                        {
                            new Label
                            {
                                StyleClasses = { "LabelHeading" },
                                Text = Loc.GetString("late-join-gui-faction-header"),
                                Align = Label.AlignMode.Center,
                                Margin = new Thickness(10, 8)
                            }
                        }
                    }
                }
            });

            // Описание
            _nibiruDescription = new Robust.Client.UserInterface.Controls.RichTextLabel
            {
                Margin = new Thickness(15, 10),
                HorizontalAlignment = HAlignment.Center
            };
            _nibiruDescription.SetMessage(Loc.GetString("late-join-gui-faction-description"));
            baseContainer.AddChild(_nibiruDescription);

            // Список фракций
            _factionList = new BoxContainer
            {
                Orientation = LayoutOrientation.Vertical,
                Margin = new Thickness(10, 5),
                VerticalExpand = true
            };

            _factionScroll = new ScrollContainer
            {
                VerticalExpand = true,
                HorizontalExpand = true,
                Children = { _factionList }
            };

            baseContainer.AddChild(_factionScroll);

            // Список персонажей
            _characterList = new BoxContainer
            {
                Orientation = LayoutOrientation.Vertical,
                Margin = new Thickness(10, 5),
                VerticalExpand = true
            };

            _characterScroll = new ScrollContainer
            {
                VerticalExpand = true,
                HorizontalExpand = true,
                Children = { _characterList },
                Visible = false
            };

            baseContainer.AddChild(_characterScroll);

            // Кнопка одиночного спавна
            _soloButton = new Button
            {
                Text = Loc.GetString("late-join-gui-spawn-solo"),
                HorizontalAlignment = HAlignment.Center,
                Margin = new Thickness(10, 5),
                MinSize = new Vector2(250, 40),
                StyleClasses = { "ButtonSquare" }
            };
            _soloButton.OnPressed += _ =>
            {
                OnFactionSelected(null);
            };
            baseContainer.AddChild(_soloButton);

            Contents.AddChild(baseContainer);

            // Подписываемся на обновления
            _gameTicker.AvailableFactionsUpdated += UpdateAvailableFactions;
            _gameTicker.SavedCharactersAvailableUpdated += _ => RebuildUI();
            _prefsManager.OnPreferencesChanged += RebuildUI;

            // Обновляем UI при создании
            RebuildUI();
        }

        protected override void Opened()
        {
            base.Opened();
            // Запрашиваем актуальную информацию о сохраненном персонаже при открытии
            _gameTicker.RequestSavedCharacter();
            RebuildUI();
        }

        /// <summary>
        /// Перестраивает UI, читая текущий список фракций из GameTicker
        /// </summary>
        private void RebuildUI()
        {
            var savedCharacters = _gameTicker.SavedCharacters;
            bool hasSaved = savedCharacters.Count > 0;

            if (hasSaved)
            {
                _factionScroll.Visible = false;
                _soloButton.Visible = false;
                _characterScroll.Visible = true;
                _nibiruDescription.SetMessage(Loc.GetString("late-join-gui-faction-description-loaded"));

                _characterList.RemoveAllChildren();
                foreach (var characterName in savedCharacters)
                {
                    var btn = new Button
                    {
                        Text = Loc.GetString("late-join-gui-load-button", ("character", characterName)),
                        HorizontalAlignment = HAlignment.Center,
                        Margin = new Thickness(10, 5),
                        MinSize = new Vector2(250, 40),
                        StyleClasses = { "ButtonSquare" },
                        ModulateSelfOverride = Color.LimeGreen
                    };
                    btn.OnPressed += _ =>
                    {
                        _consoleHost.ExecuteCommand($"latejoin_load \"{characterName}\"");
                        Close();
                    };
                    _characterList.AddChild(btn);
                }

                // Кнопка "Начать нового персонажа"
                var selectedCharacterName = _prefsManager.Preferences?.SelectedCharacter?.Name;
                bool profileInSave = selectedCharacterName != null && savedCharacters.Contains(selectedCharacterName);

                var newBtn = new Button
                {
                    Text = Loc.GetString("late-join-gui-new-character-button"),
                    HorizontalAlignment = HAlignment.Center,
                    Margin = new Thickness(10, 20),
                    MinSize = new Vector2(250, 40),
                    Visible = !profileInSave
                };
                newBtn.OnPressed += _ =>
                {
                    _characterScroll.Visible = false;
                    _factionScroll.Visible = true;
                    _soloButton.Visible = true;
                    _nibiruDescription.SetMessage(Loc.GetString("late-join-gui-faction-description"));
                };
                _characterList.AddChild(newBtn);
            }
            else
            {
                _factionScroll.Visible = true;
                _soloButton.Visible = true;
                _characterScroll.Visible = false;
                _nibiruDescription.SetMessage(Loc.GetString("late-join-gui-faction-description"));
            }

            // Читаем список напрямую из GameTicker
            UpdateAvailableFactions(_gameTicker.AvailableFactions);
        }

        /// <summary>
        /// Вызывается когда игрок выбирает фракцию или одиночный спавн
        /// </summary>
        private void OnFactionSelected(string? factionName)
        {
            if (factionName != null)
            {
                _consoleHost.ExecuteCommand($"latejoin_faction \"{factionName}\"");
                _sawmill.Info($"Player chose faction: {factionName}");
            }
            else
            {
                _consoleHost.ExecuteCommand($"latejoin_solo");
                _sawmill.Info("Player chose solo spawn");
            }

            // Закрываем окно
            Close();
        }

        /// <summary>
        /// Обновляет список доступных фракций
        /// Вызывается автоматически при получении обновления от сервера
        /// </summary>
        private void UpdateAvailableFactions(IReadOnlyList<FactionInfo> availableFactions)
        {
            _factionButtons.Clear();
            _factionList.RemoveAllChildren();

            if (availableFactions.Count == 0)
            {
                _factionList.AddChild(new PanelContainer
                {
                    Children =
                    {
                        new Label
                        {
                            Text = Loc.GetString("late-join-gui-no-factions"),
                            HorizontalAlignment = HAlignment.Center,
                            Margin = new Thickness(0, 40),
                            StyleClasses = { "LabelBig" }
                        }
                    }
                });
                return;
            }

            var profile = _prefsManager.Preferences?.SelectedCharacter as HumanoidCharacterProfile;

            // Сортируем фракции по количеству членов (сначала самые большие)
            var sortedFactions = availableFactions
                .Where(f => f.IsRecruiting) // Показываем только фракции с открытым набором
                .Where(f =>
                {
                    if (profile == null)
                        return true;

                    // Фильтр по расе
                    if (f.WhiteListSpecies.Count > 0 && !f.WhiteListSpecies.Contains(profile.Species.Id))
                        return false;

                    // Фильтр по полу
                    if (f.WhiteListGender.Count > 0 && !f.WhiteListGender.Contains(profile.Sex))
                        return false;

                    // Фильтр по цвету кожи
                    if (f.WhiteListSkinColor.Count > 0 && !f.WhiteListSkinColor.Contains(profile.Appearance.SkinColor))
                        return false;

                    // Фильтр по имени
                    if (f.WhiteListNames.Count > 0)
                    {
                        var passed = false;
                        foreach (var keyword in f.WhiteListNames)
                        {
                            if (profile.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                            {
                                passed = true;
                                break;
                            }
                        }
                        if (!passed)
                            return false;
                    }

                    return true;
                })
                .OrderByDescending(f => f.MemberCount)
                .ToList();

            if (sortedFactions.Count == 0)
            {
                _factionList.AddChild(new PanelContainer
                {
                    Children =
                    {
                        new Label
                        {
                            Text = Loc.GetString("late-join-gui-no-factions"),
                            HorizontalAlignment = HAlignment.Center,
                            Margin = new Thickness(0, 40),
                            StyleClasses = { "LabelBig" }
                        }
                    }
                });
                return;
            }

            foreach (var faction in sortedFactions)
            {
                var factionButton = CreateFactionButton(faction);
                _factionButtons[faction.FactionName] = factionButton;
                _factionList.AddChild(factionButton);

                // Добавляем отступ между кнопками
                _factionList.AddChild(new Control { MinSize = new Vector2(0, 5) });
            }
        }

        /// <summary>
        /// Создаёт кнопку для выбора фракции
        /// </summary>
        private FactionButton CreateFactionButton(FactionInfo faction)
        {
            var button = new FactionButton(faction);

            var mainContainer = new PanelContainer
            {
                StyleClasses = { "ButtonRect" },
                MinSize = new Vector2(0, 80)
            };

            var contentBox = new BoxContainer
            {
                Orientation = LayoutOrientation.Horizontal,
                HorizontalExpand = true,
                Margin = new Thickness(15, 10)
            };

            // Цветная полоска слева
            var colorBar = new PanelContainer
            {
                MinSize = new Vector2(8, 0),
                MaxSize = new Vector2(8, 1000),
                VerticalExpand = true,
                ModulateSelfOverride = faction.Color
            };
            contentBox.AddChild(colorBar);

            contentBox.AddChild(new Control { MinSize = new Vector2(10, 0) });

            // Иконка фракции
            if (!string.IsNullOrEmpty(faction.IconPath))
            {
                var iconContainer = new BoxContainer
                {
                    Orientation = LayoutOrientation.Vertical,
                    VerticalAlignment = VAlignment.Center,
                    Margin = new Thickness(0, 0, 15, 0)
                };

                var icon = new TextureRect
                {
                    TextureScale = new Vector2(2.5f, 2.5f),
                    VerticalAlignment = VAlignment.Center,
                    MinSize = new Vector2(64, 64)
                };

                try
                {
                    var spriteSpec = _prototypeManager.Index<StatusIconPrototype>(faction.IconPath);
                    icon.Texture = _sprites.Frame0(spriteSpec.Icon);
                }
                catch
                {
                    // Если иконка не найдена, используем placeholder
                    try
                    {
                        var defaultIcon = new SpriteSpecifier.Rsi(
                            new ResPath("/Textures/Interface/Misc/job_icons.rsi"),
                            "ShaftMiner");
                        icon.Texture = _sprites.Frame0(defaultIcon);
                    }
                    catch
                    {
                        // Игнорируем если не удалось загрузить
                    }
                }

                iconContainer.AddChild(icon);
                contentBox.AddChild(iconContainer);
            }

            // Информация о фракции
            var infoBox = new BoxContainer
            {
                Orientation = LayoutOrientation.Vertical,
                VerticalExpand = true,
                HorizontalExpand = true
            };

            // Название фракции
            var nameLabel = new Label
            {
                StyleClasses = { "LabelBig" },
                Text = faction.FactionName,
                FontColorOverride = faction.Color
            };
            infoBox.AddChild(nameLabel);

            // Описание
            if (!string.IsNullOrEmpty(faction.Description))
            {
                var descLabel = new Label
                {
                    Text = faction.Description,
                    FontColorOverride = Color.LightGray,
                    ClipText = true,
                    MaxWidth = 350
                };
                infoBox.AddChild(descLabel);
            }

            infoBox.AddChild(new Control { MinSize = new Vector2(0, 5) });

            // Статистика
            var statsBox = new BoxContainer
            {
                Orientation = LayoutOrientation.Horizontal
            };

            // Количество членов
            var membersLabel = new Label
            {
                Text = Loc.GetString("late-join-gui-members-count", ("count", faction.MemberCount)),
                FontColorOverride = Color.FromHex("#88c0d0"),
                StyleClasses = { "LabelSmall" }
            };
            statsBox.AddChild(membersLabel);

            // Разделитель
            statsBox.AddChild(new Control { MinSize = new Vector2(20, 0) });

            // Статус фракции
            var statusLabel = new Label
            {
                Text = Loc.GetString($"late-join-gui-faction-status-{faction.Status.ToString().ToLower()}"),
                FontColorOverride = faction.Status switch
                {
                    FactionStatus.Active => Color.FromHex("#a3be8c"),
                    FactionStatus.Recruiting => Color.FromHex("#ebcb8b"),
                    FactionStatus.AtWar => Color.FromHex("#bf616a"),
                    _ => Color.White
                },
                StyleClasses = { "LabelSmall" }
            };
            statsBox.AddChild(statusLabel);

            infoBox.AddChild(statsBox);
            contentBox.AddChild(infoBox);

            mainContainer.AddChild(contentBox);
            button.AddChild(mainContainer);

            // Обработчик клика - отправляем выбор фракции
            button.OnPressed += _ => OnFactionSelected(faction.FactionName);

            return button;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing)
            {
                _gameTicker.AvailableFactionsUpdated -= UpdateAvailableFactions;
                _prefsManager.OnPreferencesChanged -= RebuildUI;
                _factionButtons.Clear();
            }
        }
    }

    /// <summary>
    /// Кнопка выбора фракции
    /// </summary>
    sealed class FactionButton : ContainerButton
    {
        public FactionInfo Faction { get; }

        public FactionButton(FactionInfo faction)
        {
            Faction = faction;
            AddStyleClass(StyleClassButton);
            VerticalExpand = false;
            HorizontalExpand = true;
        }
    }
}

using System.Linq;
using System.Numerics;
using Content.Client.GameTicking.Managers;
using Content.Client.UserInterface.Controls;
using Content.Shared._Nibiru.Factions;
using Content.Shared.StatusIcon;
using Robust.Client.Console;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
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
        private readonly Button _soloButton;

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
            var descLabel = new RichTextLabel
            {
                Margin = new Thickness(15, 10),
                HorizontalAlignment = HAlignment.Center
            };
            descLabel.SetMessage(Loc.GetString("late-join-gui-faction-description"));
            baseContainer.AddChild(descLabel);

            // Список фракций
            _factionList = new BoxContainer
            {
                Orientation = LayoutOrientation.Vertical,
                Margin = new Thickness(10, 5),
                VerticalExpand = true
            };

            var scrollContainer = new ScrollContainer
            {
                VerticalExpand = true,
                HorizontalExpand = true,
                Children = { _factionList }
            };

            baseContainer.AddChild(scrollContainer);

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
                _sawmill.Info("Player chose solo spawn");
                OnFactionSelected(null);
                _consoleHost.ExecuteCommand($"latejoin_solo");
            };
            baseContainer.AddChild(_soloButton);

            Contents.AddChild(baseContainer);

            // Подписываемся на обновления списка фракций от GameTicker
            _gameTicker.AvailableFactionsUpdated += UpdateAvailableFactions;

            // Обновляем UI при открытии
            RebuildUI();
        }

        /// <summary>
        /// Перестраивает UI, читая текущий список фракций из GameTicker
        /// </summary>
        private void RebuildUI()
        {
            // Читаем список напрямую из GameTicker (как оригинальный LateJoinGui читает JobsAvailable)
            UpdateAvailableFactions(_gameTicker.AvailableFactions);
        }

        /// <summary>
        /// Вызывается когда игрок выбирает фракцию или одиночный спавн
        /// </summary>
        private void OnFactionSelected(string? factionName)
        {
            _consoleHost.ExecuteCommand($"latejoin_faction {factionName}");
            _consoleHost.ExecuteCommand($"latejoin_solo");

            if (factionName != null)
                _sawmill.Info($"Player chose faction: {factionName}");
            else
                _sawmill.Info("Player chose solo spawn");

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

            // Сортируем фракции по количеству членов (сначала самые большие)
            var sortedFactions = availableFactions
                .Where(f => f.IsRecruiting) // Показываем только фракции с открытым набором
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
                            "Unknown");
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

            // Стрелка справа
            var arrowBox = new BoxContainer
            {
                VerticalAlignment = VAlignment.Center,
                HorizontalAlignment = HAlignment.Right,
                Margin = new Thickness(10, 0, 0, 0)
            };

            var arrow = new TextureRect
            {
                TextureScale = new Vector2(1.5f, 1.5f),
                VerticalAlignment = VAlignment.Center,
                Modulate = Color.FromHex("#4c566a")
            };

            try
            {
                arrow.Texture = _sprites.Frame0(new SpriteSpecifier.Texture(
                    new ResPath("/Textures/Interface/Nano/triangle_right.svg.192dpi.png")));
            }
            catch
            {
                // Игнорируем если не удалось загрузить
            }

            arrowBox.AddChild(arrow);
            contentBox.AddChild(arrowBox);

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

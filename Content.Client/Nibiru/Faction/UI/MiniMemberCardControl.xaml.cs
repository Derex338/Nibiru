using Robust.Client.UserInterface.Controls;
using Robust.Client.GameObjects;
using Robust.Shared.IoC;
using Content.Shared.IdentityManagement;
using Content.Shared._Nibiru.Factions;
using Robust.Client.UserInterface;
using System.Numerics;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.Nibiru.Faction.UI;

public sealed partial class MiniMemberCardControl : Control
{
    private readonly PanelContainer _background;
    private readonly Button _mainButton;
    private readonly SpriteView _spriteView;
    private readonly RichTextLabel _nameLabel;
    private readonly Label _rankLabel;
    private readonly Button _kickButton;
    private readonly Button _rankButton;

    public MiniMemberCardControl(EntityUid member,
                                EntityUid? playerEntity,
                                IEntityManager entityManager)
    {
        // Главный контейнер
        var mainContainer = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal
        };

        // Фоновая панель (цветная полоска слева)
        _background = new PanelContainer
        {
            StyleClasses = { "PdaBackground" },
            VerticalExpand = false,
            HorizontalExpand = false,
            MaxWidth = 10,
            Margin = new Thickness(0, 0, -5, 0)
        };

        // Главная кнопка
        _mainButton = new Button
        {
            Disabled = true,
            HorizontalExpand = true,
            VerticalExpand = false,
            StyleClasses = { "ButtonSquare" },
            Margin = new Thickness(0)
        };

        // Содержимое главной кнопки
        var buttonContent = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            Margin = new Thickness(5)
        };

        // Спрайт персонажа
        _spriteView = new SpriteView
        {
            OverrideDirection = Direction.South,
            Scale = new Vector2(2, 2),
            SetSize = new Vector2(32, 32)
        };
        _spriteView.SetEntity(member);

        // Отступ
        var spacer = new Control
        {
            MinWidth = 5
        };

        // Контейнер для имени и ранга
        var infoContainer = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            VerticalAlignment = VAlignment.Center,
            HorizontalExpand = true
        };

        // Имя персонажа
        _nameLabel = new RichTextLabel
        {
            StyleClasses = { "LabelSubText" }
        };
        _nameLabel.SetMessage(Identity.Name(member, entityManager));

        // Ранг
        _rankLabel = new Label
        {
            StyleClasses = { "LabelSmall" },
            FontColorOverride = Color.Gray
        };

        if (entityManager.TryGetComponent<FactionComponent>(member, out var memberFaction))
        {
            _rankLabel.Text = string.IsNullOrEmpty(memberFaction.Rank)
                ? "Без ранга"
                : memberFaction.Rank;
        }

        infoContainer.AddChild(_nameLabel);
        infoContainer.AddChild(_rankLabel);

        // Кнопка изменения ранга
        _rankButton = new Button
        {
            StyleClasses = { "ButtonSquare" },
            HorizontalAlignment = HAlignment.Right,
            Text = "🏷️",
            MinWidth = 32,
            ToolTip = "Изменить ранг"
        };

        // Кнопка кика
        _kickButton = new Button
        {
            StyleClasses = { "Caution" },
            HorizontalAlignment = HAlignment.Right,
            Text = "✕",
            MinWidth = 32,
            ToolTip = "Исключить из фракции"
        };

        // Добавляем кнопки только если это не сам игрок
        if (playerEntity != null && playerEntity != member)
        {
            _rankButton.OnPressed += _ => ChangeRank(member, playerEntity, entityManager);
            _kickButton.OnPressed += _ => KickMember(member, playerEntity, entityManager);
        }
        else
        {
            _rankButton.Visible = false;
            _kickButton.Visible = false;
        }

        // Собираем содержимое кнопки
        buttonContent.AddChild(_spriteView);
        buttonContent.AddChild(spacer);
        buttonContent.AddChild(infoContainer);
        buttonContent.AddChild(_rankButton);
        buttonContent.AddChild(_kickButton);

        // Добавляем содержимое в главную кнопку
        _mainButton.AddChild(buttonContent);

        // Собираем всё вместе
        mainContainer.AddChild(_background);
        mainContainer.AddChild(_mainButton);

        // Добавляем главный контейнер в Control
        AddChild(mainContainer);
    }

    private void ChangeRank(EntityUid member, EntityUid? playerEntity, IEntityManager entityManager)
    {
        if (playerEntity == null)
            return;

        // Проверяем права на изменение ранга
        if (!entityManager.TryGetComponent<FactionComponent>(playerEntity.Value, out var playerFaction))
            return;

        if (!playerFaction.IsCreator)
            return;

        // Открываем диалог ввода ранга
        var prompt = new RankChangePrompt(member, entityManager);
        //prompt.OpenCentered();
    }

    private void KickMember(EntityUid member, EntityUid? playerEntity, IEntityManager entityManager)
    {
        if (playerEntity == null)
            return;

        // Проверяем права на кик
        if (!entityManager.TryGetComponent<FactionComponent>(member, out var playerMember))
            return;

        if (!entityManager.TryGetComponent<FactionComponent>(playerEntity, out var faction))
            return;

        // Проверяем, является ли игрок создателем фракции
        if (!faction.IsCreator)
            return;

        // Отправляем сообщение о кике
        entityManager.RaisePredictiveEvent(new FactionKickMemberMessage
        {
            Member = entityManager.GetNetEntity(member)
        });

        // Удаляем карточку из UI
        Parent?.RemoveChild(this);
    }

    // Публичные свойства для доступа к элементам
    public PanelContainer Background => _background;
    public SpriteView SpriteView => _spriteView;

    // Метод для изменения цвета фона
    public void SetBackgroundColor(Color color)
    {
        _background.ModulateSelfOverride = color;
    }

    // Метод для обновления ранга
    public void UpdateRank(string rank)
    {
        _rankLabel.Text = string.IsNullOrEmpty(rank) ? "Без ранга" : rank;
    }

    // Метод для внешнего удаления
    public void Remove()
    {
        Parent?.RemoveChild(this);
    }
}

/// <summary>
/// Диалог для изменения ранга члена фракции
/// </summary>
public sealed class RankChangePrompt : DefaultWindow
{
    private readonly EntityUid _member;
    private readonly IEntityManager _entityManager;
    private readonly LineEdit _rankInput;

    public RankChangePrompt(EntityUid member, IEntityManager entityManager)
    {
        _member = member;
        _entityManager = entityManager;

        Title = "Изменить ранг";
        MinSize = new Vector2(300, 120);

        var container = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(10)
        };

        container.AddChild(new Label
        {
            Text = "Введите новый ранг:",
            Margin = new Thickness(0, 0, 0, 5)
        });

        _rankInput = new LineEdit
        {
            PlaceHolder = "Например: Офицер",
            HorizontalExpand = true
        };

        // Если у члена уже есть ранг, показываем его
        if (entityManager.TryGetComponent<FactionComponent>(member, out var faction))
        {
            _rankInput.Text = faction.Rank;
        }

        container.AddChild(_rankInput);

        var buttonsContainer = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalAlignment = HAlignment.Right,
            Margin = new Thickness(0, 10, 0, 0)
        };

        var confirmButton = new Button
        {
            Text = "Подтвердить",
            MinSize = new Vector2(100, 30)
        };
        confirmButton.OnPressed += _ => OnConfirm();

        var cancelButton = new Button
        {
            Text = "Отмена",
            MinSize = new Vector2(100, 30),
            Margin = new Thickness(5, 0, 0, 0)
        };
        cancelButton.OnPressed += _ => Close();

        buttonsContainer.AddChild(confirmButton);
        buttonsContainer.AddChild(cancelButton);

        container.AddChild(buttonsContainer);

        Contents.AddChild(container);

        _rankInput.OnTextEntered += _ => OnConfirm();
    }

    private void OnConfirm()
    {
        var newRank = _rankInput.Text.Trim();

        if (string.IsNullOrEmpty(newRank))
            newRank = "Без ранга";

        _entityManager.RaisePredictiveEvent(new FactionChangeMemberRankMessage
        {
            Member = _entityManager.GetNetEntity(_member),
            NewRank = newRank
        });

        Close();
    }
}

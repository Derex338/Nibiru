using Robust.Client.UserInterface.Controls;
using Robust.Client.GameObjects;
using Robust.Shared.IoC;
using Content.Shared.IdentityManagement;
using Content.Shared._Nibiru.Factions;
using Robust.Client.UserInterface;
using System.Numerics;

namespace Content.Client.Nibiru.Faction.UI;

public sealed partial class MiniMemberCardControl : Control
{
    private readonly PanelContainer _background;
    private readonly Button _mainButton;
    private readonly SpriteView _spriteView;
    private readonly RichTextLabel _nameLabel;
    private readonly Button _kickButton;

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
            Margin = new Thickness(0)
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

        // Имя персонажа
        _nameLabel = new RichTextLabel
        {
            StyleClasses = { "LabelSubText" },
            VerticalAlignment = VAlignment.Center
        };
        _nameLabel.SetMessage(Identity.Name(member, entityManager));

        // Кнопка кика
        _kickButton = new Button
        {
            StyleClasses = { "Caution" },
            HorizontalAlignment = HAlignment.Right,
            Text = "✕", // Символ крестика
            MinWidth = 32
        };

        // Добавляем кнопку кика только если это не сам игрок
        if (playerEntity != null && playerEntity != member)
        {
            _kickButton.OnPressed += _ => KickMember(member, playerEntity, entityManager);
        }
        else
        {
            _kickButton.Visible = false;
        }

        // Собираем содержимое кнопки
        buttonContent.AddChild(_spriteView);
        buttonContent.AddChild(spacer);
        buttonContent.AddChild(_nameLabel);
        buttonContent.AddChild(_kickButton);

        // Добавляем содержимое в главную кнопку
        _mainButton.AddChild(buttonContent);

        // Собираем всё вместе
        mainContainer.AddChild(_background);
        mainContainer.AddChild(_mainButton);

        // Добавляем главный контейнер в Control
        AddChild(mainContainer);
    }

    private void KickMember(EntityUid member, EntityUid? playerEntity, IEntityManager entityManager)
    {
        if (playerEntity == null)
            return;

        // Проверяем права на кик
        if (!entityManager.TryGetComponent<FactionComponent>(playerEntity.Value, out var playerMember))
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
        //Dispose();
    }

    // Публичные свойства для доступа к элементам (если нужно)
    public PanelContainer Background => _background;
    public SpriteView SpriteView => _spriteView;

    // Метод для изменения цвета фона (если нужно)
    public void SetBackgroundColor(Color color)
    {
        _background.ModulateSelfOverride = color;
    }

    // Метод для внешнего удаления
    public void Remove()
    {
        Parent?.RemoveChild(this);
        //Dispose();
    }
}

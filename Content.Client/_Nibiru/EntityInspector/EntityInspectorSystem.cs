using Content.Shared._Nibiru.EntityInspector;
using Content.Shared.Verbs;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Shared.Utility;

namespace Content.Client._Nibiru.EntityInspector;

/// <summary>
/// Клиентская система инспектора сущностей.
/// Добавляет иконку-кнопку в окно осмотра предмета (в тултип осмотра)
/// для любой сущности, у которой хотя бы один компонент реализует
/// <see cref="IInspectableComponent"/>.
/// </summary>
[UsedImplicitly]
public sealed class EntityInspectorSystem : EntitySystem
{
    [Dependency] private readonly IUserInterfaceManager _uiManager     = default!;
    [Dependency] private readonly IEntityManager        _entityManager = default!;
    [Dependency] private readonly Robust.Client.Player.IPlayerManager _playerManager = default!;

    /// <summary>Переиспользуемое окно инспектора.</summary>
    private EntityInspectorWindow? _window;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GetVerbsEvent<ExamineVerb>>(OnGetExamineVerbs);
    }

    // ──────────────────────────────────────────────────────────
    //  Кнопка в тултипе осмотра
    // ──────────────────────────────────────────────────────────

    private void OnGetExamineVerbs(GetVerbsEvent<ExamineVerb> args)
    {
        if (!HasInspectableComponent(args.Target))
            return;

        args.Verbs.Add(new ExamineVerb
        {
            ShowOnExamineTooltip = true,   // ← появляется как иконка в тултипе осмотра
            HoverVerb            = false,  // ← кликабельная кнопка, не hover
            ClientExclusive      = true,
            Priority             = 0,

            Text = Loc.GetString("entity-inspector-examine-verb"),
            Icon = new SpriteSpecifier.Texture(
                new ResPath("/Textures/Interface/VerbIcons/examine.svg.192dpi.png")),

            Act = () => OpenInspector(args.Target),
        });
    }

    // ──────────────────────────────────────────────────────────
    //  Открытие / обновление окна
    // ──────────────────────────────────────────────────────────

    private void OpenInspector(EntityUid target)
    {
        // Если окно уничтожено или ещё не создано — создаём заново
        if (_window is null || _window.Disposed)
            _window = _uiManager.CreateWindow<EntityInspectorWindow>();

        // Если закрыто (крестиком) но не уничтожено — открываем снова
        if (!_window.IsOpen)
            _window.OpenCentered();

        _window.MoveToFront();
        PopulateWindow(target);
    }

    private void PopulateWindow(EntityUid target)
    {
        if (_window is null || _window.Disposed)
            return;

        var entityName = _entityManager.GetComponent<MetaDataComponent>(target).EntityName;
        var isOwner = IsOwnedByPlayer(target);

        var allComponents = new List<IComponent>();
        foreach (var comp in _entityManager.GetComponents(target))
            allComponents.Add(comp);

        _window.Populate(entityName, allComponents, isOwner);
    }

    // ──────────────────────────────────────────────────────────
    //  Вспомогательные (без рефлексии)
    // ──────────────────────────────────────────────────────────

    private bool IsOwnedByPlayer(EntityUid target)
    {
        var player = _playerManager.LocalEntity;
        if (player == null) return false;

        // Является ли игрок самим собой (смотрим на себя)
        if (target == player.Value)
            return true;

        // Находится ли предмет в инвентаре/руках/внутри игрока (рекурсивный поиск родителя)
        var current = target;
        while (_entityManager.TryGetComponent<TransformComponent>(current, out var xform) && xform.ParentUid.Valid)
        {
            if (xform.ParentUid == player.Value)
                return true;
            current = xform.ParentUid;
        }

        return false;
    }

    private bool HasInspectableComponent(EntityUid entity)
    {
        foreach (var comp in _entityManager.GetComponents(entity))
        {
            if (comp is IInspectableComponent)
                return true;
        }
        return false;
    }
}

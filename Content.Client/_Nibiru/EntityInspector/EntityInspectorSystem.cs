using Content.Shared._Nibiru.EntityInspector;
using Content.Shared.Verbs;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Shared.Utility;

namespace Content.Client._Nibiru.EntityInspector;

/// <summary>
/// Rimworld like inspector system
/// </summary>
[UsedImplicitly]
public sealed partial class EntityInspectorSystem : EntitySystem
{
    [Dependency] private IUserInterfaceManager _uiManager     = default!;
    [Dependency] private IEntityManager        _entityManager = default!;
    [Dependency] private Robust.Client.Player.IPlayerManager _playerManager = default!;

    private EntityInspectorWindow? _window;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GetVerbsEvent<ExamineVerb>>(OnGetExamineVerbs);
    }

    //  Eximine verb - button in examine tooltip

    private void OnGetExamineVerbs(GetVerbsEvent<ExamineVerb> args)
    {
        if (!HasInspectableComponent(args.Target))
            return;

        args.Verbs.Add(new ExamineVerb
        {
            ShowOnExamineTooltip = true,
            HoverVerb            = false,
            ClientExclusive      = true,
            Priority             = 0,

            Text = Loc.GetString("entity-inspector-examine-verb"),
            Icon = new SpriteSpecifier.Texture(
                new ResPath("/Textures/Interface/VerbIcons/examine.svg.192dpi.png")),

            Act = () => OpenInspector(args.Target),
        });
    }

    private void OpenInspector(EntityUid target)
    {
        // If window is destroyed or not created yet - create new one
        if (_window is null || _window.Disposed)
            _window = _uiManager.CreateWindow<EntityInspectorWindow>();

        // If closed (by cross) but not destroyed - open again
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

    private bool IsOwnedByPlayer(EntityUid target)
    {
        var player = _playerManager.LocalEntity;
        if (player == null) return false;

        if (target == player.Value)
            return true;

        // Is the item in inventory/hands/inside the player (recursive search for the parent)
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

using Content.Shared._Nibiru.Construction;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Popups;

namespace Content.Server._Nibiru.Construcion;

public sealed partial class MultiLevelClimbSystem : EntitySystem
{
[Dependency] private SharedTransformSystem _transform = default!;
[Dependency] private SharedDoAfterSystem _doAfter = default!;
[Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MultiLevelClimbableComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<MultiLevelClimbableComponent, MultiLevelClimbDoAfterEvent>(OnClimbDoAfter);
    }

    private void OnActivate(EntityUid uid, MultiLevelClimbableComponent component, ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex)
            return;

        var user = args.User;

        if (!TryComp<MultiLevelConstructionComponent>(uid, out var mlComp))
            return;

        EntityUid? target = null;
        var myMap = Transform(uid).MapUid;

        foreach (var other in mlComp.LinkedEntities)
        {
            var otherMap = Transform(other).MapUid;
            if (otherMap != null && otherMap != myMap)
            {
                target = other;
                break;
            }
        }

        if (target == null)
            return;

        args.Handled = true;

        var doAfterArgs = new DoAfterArgs(EntityManager, user, component.ClimbDuration, new MultiLevelClimbDoAfterEvent(), uid, uid)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            NeedHand = false
        };

        _doAfter.TryStartDoAfter(doAfterArgs);
        _popup.PopupEntity(Loc.GetString("climbable-start-climbing"), uid, user);
    }

    private void OnClimbDoAfter(EntityUid uid, MultiLevelClimbableComponent component, MultiLevelClimbDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        if (!TryComp<MultiLevelConstructionComponent>(uid, out var mlComp))
            return;

        var myMap = Transform(uid).MapUid;
        EntityUid? target = null;
        foreach (var other in mlComp.LinkedEntities)
        {
            var otherMap = Transform(other).MapUid;
            if (otherMap != null && otherMap != myMap)
            {
                target = other;
                break;
            }
        }

        if (target != null)
        {
            var targetXform = Transform(target.Value);
            _transform.SetCoordinates(args.User, targetXform.Coordinates);
            _popup.PopupEntity(Loc.GetString("climbable-finish-climbing"), target.Value, args.User);
        }

        args.Handled = true;
    }
}

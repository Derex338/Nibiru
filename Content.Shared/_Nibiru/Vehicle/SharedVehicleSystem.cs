using Content.Shared.ActionBlocker;
using Content.Shared.Actions;
using Content.Shared.Buckle.Components;
using Content.Shared.DoAfter;
using Content.Shared.DragDrop;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Content.Shared.Whitelist;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared._Nibiru.Vehicle;

public sealed class SharedVehicleSystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedMoverController _mover = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VehicleComponent, ComponentStartup>(OnMountStartup);
        SubscribeLocalEvent<VehicleComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<VehicleComponent, DragDropTargetEvent>(OnDragDrop);
        SubscribeLocalEvent<VehicleComponent, CanDropTargetEvent>(OnCanDragDrop);
        SubscribeLocalEvent<VehicleComponent, UpdateCanMoveEvent>(OnMountCanMove);
        SubscribeLocalEvent<VehicleComponent, MobStateChangedEvent>(OnMountMobStateChanged);

        SubscribeLocalEvent<VehicleComponent, MountEvent>(OnMount);
        SubscribeLocalEvent<VehicleComponent, DismountEvent>(OnDismount);
        SubscribeLocalEvent<VehicleComponent, DismountActionEvent>(OnDismountAction);

        SubscribeLocalEvent<RiderComponent, UpdateCanMoveEvent>(OnRiderCanMove);

        SubscribeLocalEvent<VehicleComponent, StrappedEvent>(OnBuckleChanged);
        SubscribeLocalEvent<VehicleComponent, UnstrappedEvent>(OnUnbuckleChanged);
    }

    private void OnMountStartup(EntityUid uid, VehicleComponent component, ComponentStartup args)
    {
        component.RiderSlot = _container.EnsureContainer<ContainerSlot>(uid, component.RiderSlotId);
        UpdateAppearance(uid, component);
    }

    private void OnGetVerbs(EntityUid uid, VehicleComponent component, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        // Проверяем, мёртв ли транспорт
        if (!component.CanMoveWhenDead && _mobState.IsDead(uid))
            return;

        var rider = component.RiderSlot.ContainedEntity;

        if (component.RiderSlot.ContainedEntity == null && CanMount(uid, args.User, component))
        {
            var mountVerb = new AlternativeVerb
            {
                Text = Loc.GetString("mount-verb-mount"),
                Act = () =>
                {
                    var doAfterArgs = new DoAfterArgs(EntityManager, args.User, component.MountDelay,
                        new MountEvent(), uid, target: uid)
                    {
                        BreakOnMove = true,
                    };
                    _doAfter.TryStartDoAfter(doAfterArgs);
                }
            };
            args.Verbs.Add(mountVerb);
        }
        else if (component.RiderSlot.ContainedEntity != null)
        {
            var dismountVerb = new AlternativeVerb
            {
                Text = Loc.GetString("mount-verb-dismount"),
                Priority = 1,
                Act = () =>
                {
                    if (args.User == component.RiderSlot.ContainedEntity)
                    {
                        TryDismount(uid, component);
                        return;
                    }

                    var doAfterArgs = new DoAfterArgs(EntityManager, args.User, component.DismountDelay,
                        new DismountEvent(), uid, target: uid)
                    {
                        BreakOnMove = true,
                    };

                    _popup.PopupEntity(
                        Loc.GetString("mount-dismount-other-alert", ("mount", uid), ("user", args.User)),
                        uid, PopupType.Large);

                    _doAfter.TryStartDoAfter(doAfterArgs);
                }
            };
            args.Verbs.Add(dismountVerb);
        }
    }

    private void OnDragDrop(EntityUid uid, VehicleComponent component, ref DragDropTargetEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (!component.CanMoveWhenDead && _mobState.IsDead(uid))
            return;

        var doAfterArgs = new DoAfterArgs(EntityManager, args.Dragged, component.MountDelay,
            new MountEvent(), uid, target: uid)
        {
            BreakOnMove = true,
        };

        _doAfter.TryStartDoAfter(doAfterArgs);
    }

    private void OnCanDragDrop(EntityUid uid, VehicleComponent component, ref CanDropTargetEvent args)
    {
        args.Handled = true;
        args.CanDrop = CanMount(uid, args.Dragged, component);
    }

    private void OnMountCanMove(EntityUid uid, VehicleComponent component, UpdateCanMoveEvent args)
    {
        // Если транспорт мёртв и не может двигаться мёртвым
        if (!component.CanMoveWhenDead && _mobState.IsDead(uid))
        {
            args.Cancel();
            return;
        }

        // Если ЕСТЬ всадник, маунт может двигаться (получая инпут через relay)
        // Если НЕТ всадника, маунт не может двигаться сам
        if (component.RiderSlot.ContainedEntity == null)
        {
            args.Cancel();
        }
    }

    private void OnMountMobStateChanged(EntityUid uid, VehicleComponent component, MobStateChangedEvent args)
    {
        // Обновляем внешний вид при изменении состояния
        UpdateAppearance(uid, component);

        // Если транспорт умер и не может двигаться мёртвым, блокируем движение
        if (!component.CanMoveWhenDead && args.NewMobState == MobState.Dead)
            _actionBlocker.UpdateCanMove(uid);
    }

    private void OnRiderCanMove(EntityUid uid, RiderComponent component, UpdateCanMoveEvent args)
    {
        // Всадник не может двигаться самостоятельно
        //args.Cancel();
    }

    private void OnMount(EntityUid uid, VehicleComponent component, MountEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        if (_whitelist.IsWhitelistFail(component.RiderWhitelist, args.User))
        {
            _popup.PopupEntity(Loc.GetString("mount-cant-mount"), args.User, args.User);
            return;
        }

        TryMount(uid, args.User, component);
        args.Handled = true;
    }

    private void OnDismount(EntityUid uid, VehicleComponent component, DismountEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        TryDismount(uid, component);
        args.Handled = true;
    }

    private void OnDismountAction(EntityUid uid, VehicleComponent component, DismountActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        TryDismount(uid, component);
    }

    #region Public API

    public bool CanMount(EntityUid mount, EntityUid rider, VehicleComponent? component = null)
    {
        if (!Resolve(mount, ref component))
            return false;

        if (component.RiderSlot.ContainedEntity != null)
            return false;

        if (!_actionBlocker.CanMove(rider))
            return false;

        if (!component.CanMoveWhenDead && _mobState.IsDead(mount))
            return false;

        return true;
    }

    public bool TryMount(EntityUid mount, EntityUid rider, VehicleComponent? component = null)
    {
        if (!Resolve(mount, ref component))
            return false;

        if (!CanMount(mount, rider, component))
            return false;

        SetupRider(mount, rider, component);
        _container.Insert(rider, component.RiderSlot);
        UpdateAppearance(mount, component);

        _actionBlocker.UpdateCanMove(mount);
        _actionBlocker.UpdateCanMove(rider);

        if (_net.IsServer)
            _popup.PopupEntity(Loc.GetString("mount-mounted", ("mount", mount)), rider, rider);

        return true;
    }

    public bool TryDismount(EntityUid mount, VehicleComponent? component = null)
    {
        if (!Resolve(mount, ref component))
            return false;

        var rider = component.RiderSlot.ContainedEntity;
        if (rider == null)
            return false;

        RemoveRider(mount, rider.Value);
        _container.RemoveEntity(mount, rider.Value);
        UpdateAppearance(mount, component);

        _actionBlocker.UpdateCanMove(mount);
        _actionBlocker.UpdateCanMove(rider.Value);

        if (_net.IsServer)
            _popup.PopupEntity(Loc.GetString("mount-dismounted"), rider.Value, rider.Value);

        return true;
    }

    #endregion

    private void SetupRider(EntityUid mount, EntityUid rider, VehicleComponent component)
    {
        var riderComp = EnsureComp<RiderComponent>(rider);
        var irelay = EnsureComp<InteractionRelayComponent>(rider);

        _mover.SetRelay(rider, mount);
        riderComp.Mount = mount;
        Dirty(rider, riderComp);

        _actionBlocker.UpdateCanMove(mount);
        _actionBlocker.UpdateCanMove(rider);

        if (_net.IsClient)
            return;

        _actions.AddAction(rider, ref component.DismountActionEntity, component.DismountAction, mount);
    }

    private void RemoveRider(EntityUid mount, EntityUid rider)
    {
        if (!RemComp<RiderComponent>(rider))
            return;

        RemComp<RelayInputMoverComponent>(rider);
        RemComp<InteractionRelayComponent>(rider);

        _actions.RemoveProvidedActions(rider, mount);

        _actionBlocker.UpdateCanMove(mount);
        _actionBlocker.UpdateCanMove(rider);
    }

    private void UpdateAppearance(EntityUid uid, VehicleComponent? component = null,
        AppearanceComponent? appearance = null)
    {
        if (!Resolve(uid, ref component, ref appearance, false))
            return;

        var mounted = component.RiderSlot.ContainedEntity != null;
        var dead = _mobState.IsDead(uid);

        _appearance.SetData(uid, MountVisuals.Mounted, mounted, appearance);
        _appearance.SetData(uid, MountVisuals.Dead, dead, appearance);
    }

    private void OnBuckleChanged(EntityUid uid, VehicleComponent component, ref StrappedEvent args)
    {
        // Проверяем, есть ли уже всадник
        if (component.RiderSlot.ContainedEntity == null)
        {
            TryMount(uid, args.Buckle.Owner, component);
            var riderComp = EnsureComp<RiderComponent>(args.Buckle.Owner);
            riderComp.Mount = uid;
        }
    }

    private void OnUnbuckleChanged(EntityUid uid, VehicleComponent component, ref UnstrappedEvent args)
    {
        TryDismount(uid, component);
        RemComp<RiderComponent>(args.Buckle.Owner);
    }
}

[Serializable, NetSerializable]
public sealed partial class MountEvent : SimpleDoAfterEvent
{
}

[Serializable, NetSerializable]
public sealed partial class DismountEvent : SimpleDoAfterEvent
{
}

public sealed partial class DismountActionEvent : InstantActionEvent
{
}

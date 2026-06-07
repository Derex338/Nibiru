using Content.Shared._Nibiru.Vehicle;
using Content.Shared.ActionBlocker;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.Damage.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DragDrop;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Components;
using Content.Shared.Inventory;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.NPC;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Physics;
using Content.Shared.Inventory.Events;

namespace Content.Shared.Movement.Systems;

/// <summary>
/// Система управления транспортом через StrapComponent
/// </summary>
public sealed class RideableSystem : EntitySystem
{
    [Dependency] private readonly ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedMoverController _mover = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedBuckleSystem _buckle = default!;
    [Dependency] private readonly InventorySystem _inventorySystem = default!;
    [Dependency] private readonly PullingSystem _pull = default!;
    [Dependency] private readonly SharedHandsSystem _hand = default!;
    [Dependency] private readonly SharedStaminaSystem _stamina = default!;
    [Dependency] private readonly SharedPhysicsSystem _phys = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RideableComponent, StrappedEvent>(OnBuckleChange);
        SubscribeLocalEvent<RideableComponent, UnstrappedEvent>(OnUnBuckleChange);

        SubscribeLocalEvent<RideableComponent, ComponentStartup>(OnRideableStartup);
        SubscribeLocalEvent<RideableComponent, UpdateCanMoveEvent>(OnRideableCanMove);
        SubscribeLocalEvent<RideableComponent, MobStateChangedEvent>(OnRideableMobStateChanged);

        SubscribeLocalEvent<RideableComponent, DragDropTargetEvent>(OnDragDrop);
        SubscribeLocalEvent<RideableComponent, CanDropTargetEvent>(OnCanDragDrop);
        SubscribeLocalEvent<CanVehiclePullComponent, CanDragEvent>(OnCanDrag);

        SubscribeLocalEvent<RideableComponent, BeforeDamageChangedEvent>(OnDamageVehicle);

        SubscribeLocalEvent<SaddleComponent, GotUnequippedEvent>(OnUnequipped);
        SubscribeLocalEvent<SaddleComponent, GotEquippedEvent>(OnEquipped);
    }

    private void OnUnequipped(EntityUid uid, SaddleComponent comp, GotUnequippedEvent args)
    {
        if (TryComp<StrapComponent>(args.EquipTarget, out var strap))
        {
            foreach (var rider in strap.BuckledEntities)
            {
                RemoveRider(args.EquipTarget, rider, strap);
            }
        }

        _actionBlocker.UpdateCanMove(args.EquipTarget);
    }

    private void OnEquipped(EntityUid uid, SaddleComponent comp, GotEquippedEvent args)
    {
        if (TryComp<StrapComponent>(args.EquipTarget, out var strap) && TryComp<RideableComponent>(args.EquipTarget, out var ride))
        {
            foreach (var rider in strap.BuckledEntities)
            {
                SetupRider(args.EquipTarget, rider, ride);
            }
        }

        _actionBlocker.UpdateCanMove(args.EquipTarget);
    }

    private void OnRideableStartup(EntityUid uid, RideableComponent component, ComponentStartup args)
    {
        UpdateAppearance(uid, component);
    }

    private void OnBuckleChange(EntityUid uid, RideableComponent component, ref StrappedEvent args)
    {
        // Кто-то пристегнулся - устанавливаем управление
        SetupRider(uid, args.Buckle, component);

        UpdateAppearance(uid, component);
        _actionBlocker.UpdateCanMove(uid);
        _actionBlocker.UpdateCanMove(args.Buckle);
    }

    private void OnUnBuckleChange(EntityUid uid, RideableComponent component, ref UnstrappedEvent args)
    {
        RemoveRider(uid, args.Buckle, args.Strap.Comp);

        UpdateAppearance(uid, component);
        _actionBlocker.UpdateCanMove(uid);
        _actionBlocker.UpdateCanMove(args.Buckle);
    }

    private void OnRideableCanMove(EntityUid uid, RideableComponent component, UpdateCanMoveEvent args)
    {
        if (TryComp<StrapComponent>(uid, out var strap) && strap.BuckledEntities.Count == 0)
        {
            return;
        }

        if (!component.CanMoveWhenDead && _mobState.IsIncapacitated(uid))
        {
            args.Cancel();
            return;
        }

        if (component.NeedSeddle &&
            TryComp<InventoryComponent>(args.Uid, out var inventory) &&
            !_inventorySystem.TryGetInventoryEntity<SaddleComponent>((args.Uid, inventory), out var saddle))
        {
            args.Cancel();
            return;
        }
    }

    private void OnRideableMobStateChanged(EntityUid uid, RideableComponent component, MobStateChangedEvent args)
    {
        UpdateAppearance(uid, component);

        if (TryComp<StrapComponent>(uid, out var strap))
        {
            foreach (var rider in strap.BuckledEntities)
            {
                _stamina.TakeStaminaDamage(rider, 100, ignoreResist: true);
            }
            _buckle.StrapSetEnabled(uid, false, strap);
        }

        // Если транспорт умер, обновляем возможность движения
        if (!component.CanMoveWhenDead && _mobState.IsIncapacitated(uid))
            _actionBlocker.UpdateCanMove(uid);
    }

    private void SetupRider(EntityUid rideable, EntityUid rider, RideableComponent component)
    {
        if (component.NeedSeddle &&
            TryComp<InventoryComponent>(rideable, out var inventory) &&
            !_inventorySystem.TryGetInventoryEntity<SaddleComponent>((rideable, inventory), out var saddle))
        {
            return;
        }

        var riderComp = EnsureComp<RiderComponent>(rider);
        var irelay = EnsureComp<InteractionRelayComponent>(rider);

        if (TryComp<ActiveNPCComponent>(rideable, out var npcComp))
            RemComp<ActiveNPCComponent>(rideable);

        if (TryComp<BuckleComponent>(rider, out var buckleComp))
            _buckle.BuckleVehicleChange(rider, buckleComp, true);

        // Перенаправляем инпут от всадника к транспорту
        _mover.SetRelay(rider, rideable);
        riderComp.Rideable = rideable;
        Dirty(rider, riderComp);

        // костыль ебаный но другое не придумал
        if (TryComp<FixturesComponent>(rideable, out var fixtures))
        {
            foreach (var (id, fixture) in fixtures.Fixtures)
            {
                _phys.SetCollisionLayer(rideable, id, fixture, 0, fixtures);
                break;
            }
        }
    }

    private void RemoveRider(EntityUid rideable, EntityUid rider, StrapComponent comp)
    {
        //EnsureComp<ActiveNPCComponent>(rideable);

        RemComp<RelayInputMoverComponent>(rider);
        RemComp<InteractionRelayComponent>(rider);

        if (comp.BuckledEntities.Count > 0)
            return;

        if (!RemComp<RiderComponent>(rider))
            return;

        if (TryComp<BuckleComponent>(rider, out var buckleComp))
            _buckle.BuckleVehicleChange(rider, buckleComp, true);

        // костыль ебаный но другое не придумал
        if (TryComp<FixturesComponent>(rideable, out var fixtures))
        {
            foreach (var (id, fixture) in fixtures.Fixtures)
            {
                _phys.SetCollisionLayer(rideable, id, fixture, 65, fixtures);
                break;
            }
        }
    }

    private void UpdateAppearance(EntityUid uid, RideableComponent? component = null,
        AppearanceComponent? appearance = null)
    {
        if (!Resolve(uid, ref component, ref appearance, false))
            return;

        var mounted = false;
        if (TryComp<StrapComponent>(uid, out var strap))
            mounted = strap.BuckledEntities.Count > 0;

        var dead = _mobState.IsDead(uid);

        _appearance.SetData(uid, RideableVisuals.Mounted, mounted, appearance);
        _appearance.SetData(uid, RideableVisuals.Dead, dead, appearance);
    }

    private void OnCanDrag(Entity<CanVehiclePullComponent> ent, ref CanDragEvent args)
    {
        args.Handled = true;
    }

    private void OnCanDragDrop(EntityUid uid, RideableComponent component, ref CanDropTargetEvent args)
    {
        if (!HasComp<CanVehiclePullComponent>(args.Dragged) && !HasComp<PullerComponent>(uid))
        {
            args.CanDrop = false;
            args.Handled = true;
            return;
        }

        args.CanDrop = true;
        args.Handled = true;
    }

    private void OnDragDrop(EntityUid uid, RideableComponent component, ref DragDropTargetEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<CanVehiclePullComponent>(args.Dragged, out var draggedComp))
            return;

        _pull.TryStartPull(uid, args.Dragged);

        args.Handled = true;
    }

    private void OnDamageVehicle(EntityUid uid, RideableComponent comp, ref BeforeDamageChangedEvent args)
    {
        if (TryComp<StrapComponent>(uid, out var strap) && args.Origin is not null)
        {
            foreach (var rider in strap.BuckledEntities)
            {
                if (rider == args.Origin)
                {
                    args.Cancelled = true;
                }
            }
        }
    }
}

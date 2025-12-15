using Content.Shared.ActionBlocker;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.Interaction.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;

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

    public override void Initialize()
    {
        base.Initialize();

        // Подписываемся на события пристёгивания
        SubscribeLocalEvent<RideableComponent, StrappedEvent>(OnBuckleChange);
        SubscribeLocalEvent<RideableComponent, UnstrappedEvent>(OnUnBuckleChange);

        SubscribeLocalEvent<RideableComponent, ComponentStartup>(OnRideableStartup);
        SubscribeLocalEvent<RideableComponent, UpdateCanMoveEvent>(OnRideableCanMove);
        SubscribeLocalEvent<RideableComponent, MobStateChangedEvent>(OnRideableMobStateChanged);
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
        //_actionBlocker.UpdateCanMove(uid);
        //_actionBlocker.UpdateCanMove(args.BuckledEntity);
    }

    private void OnUnBuckleChange(EntityUid uid, RideableComponent component, ref UnstrappedEvent args)
    {
        // Кто-то отстегнулся - убираем управление
        RemoveRider(uid, args.Buckle);

        UpdateAppearance(uid, component);
        //_actionBlocker.UpdateCanMove(uid);
        //_actionBlocker.UpdateCanMove(args.BuckledEntity);
    }

    private void OnRideableCanMove(EntityUid uid, RideableComponent component, UpdateCanMoveEvent args)
    {
        // Если транспорт мёртв и не может двигаться мёртвым
        if (!component.CanMoveWhenDead && _mobState.IsDead(uid))
        {
            args.Cancel();
            return;
        }

        // Проверяем, есть ли пристёгнутый всадник
        if (!TryComp<StrapComponent>(uid, out var strap))
        {
            args.Cancel();
            return;
        }

        // Если никто не пристёгнут, транспорт не может двигаться
        if (strap.BuckledEntities.Count == 0)
        {
            args.Cancel();
        }
        // Если есть всадник - разрешаем движение (инпут идёт через relay)
    }

    private void OnRideableMobStateChanged(EntityUid uid, RideableComponent component, MobStateChangedEvent args)
    {
        UpdateAppearance(uid, component);

        // Если транспорт умер, обновляем возможность движения
        if (!component.CanMoveWhenDead && args.NewMobState == MobState.Dead)
            _actionBlocker.UpdateCanMove(uid);
    }

    private void SetupRider(EntityUid rideable, EntityUid rider, RideableComponent component)
    {
        var riderComp = EnsureComp<RiderComponent>(rider);
        var irelay = EnsureComp<InteractionRelayComponent>(rider);

        if (TryComp<BuckleComponent>(rider, out var buckleComp))
            _buckle.BuckleVehicleChange(rider, buckleComp, true);

        // Перенаправляем инпут от всадника к транспорту
        _mover.SetRelay(rider, rideable);
        riderComp.Rideable = rideable;
        Dirty(rider, riderComp);
    }

    private void RemoveRider(EntityUid rideable, EntityUid rider)
    {
        if (!RemComp<RiderComponent>(rider))
            return;

        if (TryComp<BuckleComponent>(rider, out var buckleComp))
            _buckle.BuckleVehicleChange(rider, buckleComp, true);

        RemComp<RelayInputMoverComponent>(rider);
        RemComp<InteractionRelayComponent>(rider);
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
}

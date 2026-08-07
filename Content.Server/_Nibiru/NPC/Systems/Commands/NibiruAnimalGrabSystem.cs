using Content.Shared._Nibiru.NPC.Behavior;
using Content.Shared._Nibiru.NPC.Behavior.Components;
using Content.Shared._Nibiru.NPC.Commands;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Events;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Pulling.Events;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Random;
using System.Numerics;

namespace Content.Server._Nibiru.NPC.Systems.Commands;

public sealed partial class NibiruAnimalGrabSystem : EntitySystem
{
[Dependency] private PullingSystem _pulling = default!;
[Dependency] private MovementSpeedModifierSystem _movementSpeed = default!;
[Dependency] private SharedDoAfterSystem _doAfter = default!;
[Dependency] private SharedPopupSystem _popup = default!;
[Dependency] private DamageableSystem _damageable = default!;
[Dependency] private SharedTransformSystem _xform = default!;
[Dependency] private SharedPhysicsSystem _physics = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NibiruAnimalGrabbedComponent, ComponentStartup>(OnGrabbedStartup);
        SubscribeLocalEvent<NibiruAnimalGrabbedComponent, ComponentShutdown>(OnGrabbedShutdown);
        SubscribeLocalEvent<NibiruAnimalGrabbedTargetComponent, RefreshMovementSpeedModifiersEvent>(OnTargetRefreshSpeed);
        SubscribeLocalEvent<NibiruAnimalGrabbedComponent, NibiruAnimalDetachDoAfterEvent>(OnDetachDoAfter);

        // Перехватываем попытку отпустить pull — разрешаем только через DoAfter,
        // НЕ через движение цели (животное крепко держит зубами).
        //SubscribeLocalEvent<NibiruAnimalGrabbedComponent, AttemptStopPullingEvent>(OnAnimalInteractHand);
        SubscribeLocalEvent<NibiruAnimalGrabbedComponent, PullStoppedMessage>(OnPullStop);
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  Update: тик-урон + плавная тряска жертвы через физический импульс
    // ──────────────────────────────────────────────────────────────────────────

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<NibiruAnimalGrabbedComponent>();
        while (query.MoveNext(out var animalUid, out var grabbed))
        {
            if (grabbed.Target == null || !Exists(grabbed.Target.Value))
            {
                RemComp<NibiruAnimalGrabbedComponent>(animalUid);
                continue;
            }

            var target = grabbed.Target.Value;

            // ── Тик-урон ──────────────────────────────────────────────────
            if (grabbed.TickDamage != null)
            {
                grabbed.DamageAccumulator += frameTime;
                if (grabbed.DamageAccumulator >= grabbed.DamageInterval)
                {
                    grabbed.DamageAccumulator -= grabbed.DamageInterval;
                    _damageable.TryChangeDamage(target, grabbed.TickDamage, origin: animalUid);
                }
            }

            // ── Тряска через физический импульс туда-сюда ─────────────────
            // Используем SetLinearVelocity для цели, чтобы не ломать Pull-сустав.
            grabbed.ShakeAccumulator += frameTime;
            if (grabbed.ShakeAccumulator >= grabbed.ShakeInterval)
            {
                grabbed.ShakeAccumulator -= grabbed.ShakeInterval;
                grabbed.ShakeDirection = -grabbed.ShakeDirection;

                if (TryComp(animalUid, out TransformComponent? animalXform) &&
                    TryComp(target, out TransformComponent? targetXform) &&
                    TryComp<PhysicsComponent>(target, out var targetPhys))
                {
                    var animalPos = _xform.GetWorldPosition(animalXform);
                    var targetPos = _xform.GetWorldPosition(targetXform);

                    // Перпендикуляр к вектору животное→цель
                    var toTarget = targetPos - animalPos;
                    Vector2 perp;
                    if (toTarget.LengthSquared() > 0.01f)
                    {
                        var norm = Vector2.Normalize(toTarget);
                        perp = new Vector2(-norm.Y, norm.X);
                    }
                    else
                    {
                        perp = Vector2.UnitX;
                    }

                    // Применяем боковой импульс — не кидаем, просто меняем скорость.
                    // Скорость небольшая, поэтому цель лишь слегка дёргается.
                    var shakeVelocity = perp * grabbed.ShakeAmplitude * grabbed.ShakeDirection;
                    _physics.SetLinearVelocity(target, shakeVelocity, body: targetPhys);
                }
            }
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  Component events
    // ──────────────────────────────────────────────────────────────────────────

    private void OnGrabbedStartup(EntityUid uid, NibiruAnimalGrabbedComponent component, ComponentStartup args)
    {
        if (component.Target != null)
        {
            var targetComp = EnsureComp<NibiruAnimalGrabbedTargetComponent>(component.Target.Value);
            targetComp.Grabber = uid;
            Dirty(component.Target.Value, targetComp);
            _movementSpeed.RefreshMovementSpeedModifiers(component.Target.Value);
        }
    }

    private void OnGrabbedShutdown(EntityUid uid, NibiruAnimalGrabbedComponent component, ComponentShutdown args)
    {
        if (component.Target != null && Exists(component.Target.Value))
        {
            // Сбрасываем скорость цели при отцеплении
            if (TryComp<PhysicsComponent>(component.Target.Value, out var phys))
                _physics.SetLinearVelocity(component.Target.Value, Vector2.Zero, body: phys);

            RemComp<NibiruAnimalGrabbedTargetComponent>(component.Target.Value);
            _movementSpeed.RefreshMovementSpeedModifiers(component.Target.Value);
        }
    }

    private void OnTargetRefreshSpeed(EntityUid uid, NibiruAnimalGrabbedTargetComponent component, RefreshMovementSpeedModifiersEvent args)
    {
        args.ModifySpeed(component.SlowdownMultiplier, component.SlowdownMultiplier);
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  Отцепление только через DoAfter (никакого мгновенного разрыва Pull)
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Перехватываем любую попытку остановить Pull (в том числе от движения цели).
    /// Разрешаем отцепление ТОЛЬКО через явный запрос хозяина через DoAfter.
    /// </summary>
    private void OnAnimalInteractHand(EntityUid uid, NibiruAnimalGrabbedComponent component, AttemptStopPullingEvent args)
    {
        // Если это не сама цель пытается освободиться — игнорируем,
        // но в любом случае блокируем автоматический разрыв.
        args.Cancelled = true;

        // Если взаимодействие явно от цели — предлагаем DoAfter для освобождения
        if (args.User == null || args.User != component.Target)
            return;

        var doAfterArgs = new DoAfterArgs(EntityManager, args.User.Value, component.DetachDuration,
            new NibiruAnimalDetachDoAfterEvent(), uid, uid)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true
        };

        _doAfter.TryStartDoAfter(doAfterArgs);
        _popup.PopupEntity(Loc.GetString("nibiru-animal-grab-detaching"), uid, args.User.Value);
    }

    private void OnDetachDoAfter(EntityUid uid, NibiruAnimalGrabbedComponent component, NibiruAnimalDetachDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;

        // Останавливаем тягу
        if (TryComp<PullableComponent>(uid, out var pullable))
            _pulling.TryStopPull(uid, pullable);

        // Убираем компонент захвата
        RemComp<NibiruAnimalGrabbedComponent>(uid);

        // Животное возвращается в режим Idle
        if (TryComp<NibiruNpcStateMachineComponent>(uid, out var state))
        {
            state.CurrentState = NibiruNpcState.Idle;
            state.CurrentTarget = null;
        }

        _popup.PopupEntity(Loc.GetString("nibiru-animal-grab-detached"), uid, args.User);
    }

    private void OnPullStop(EntityUid uid, NibiruAnimalGrabbedComponent component, PullStoppedMessage args)
    {
        // Если тяга была разорвана не через DoAfter — убираем компонент захвата
        if (args.PulledUid != uid)
            return;
        RemComp<NibiruAnimalGrabbedComponent>(uid);

        if (TryComp<NibiruNpcStateMachineComponent>(args.PullerUid, out var state))
        {
            state.CurrentState = NibiruNpcState.Idle;
            state.CurrentTarget = null;
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  Public API
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Заставляет животное вцепиться в цель.
    /// </summary>
    public bool TryGrabTarget(EntityUid animal, EntityUid target, DamageSpecifier? biteDamage = null)
    {
        // Наносим урон укусом
        if (biteDamage != null)
            _damageable.TryChangeDamage(target, biteDamage, origin: animal);

        // Инвертируем тягу: цель тащит животное
        if (!_pulling.TryStartPull(target, animal))
            return false;

        // Добавляем компонент захвата
        var grabbed = EnsureComp<NibiruAnimalGrabbedComponent>(animal);
        grabbed.Target = target;
        Dirty(animal, grabbed);

        return true;
    }
}

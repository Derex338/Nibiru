using Content.Shared._Nibiru.NPC.Behavior;
using Content.Shared._Nibiru.NPC.Behavior.Components;
using Content.Shared._Nibiru.NPC.Commands;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Robust.Shared.Map;
using Robust.Shared.Random;
using Robust.Shared.Serialization;
using System.Numerics;

namespace Content.Server._Nibiru.NPC.Systems.Commands;

public sealed class NibiruAnimalGrabSystem : EntitySystem
{
    [Dependency] private readonly PullingSystem _pulling = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NibiruAnimalGrabbedComponent, ComponentStartup>(OnGrabbedStartup);
        SubscribeLocalEvent<NibiruAnimalGrabbedComponent, ComponentShutdown>(OnGrabbedShutdown);
        SubscribeLocalEvent<NibiruAnimalGrabbedTargetComponent, RefreshMovementSpeedModifiersEvent>(OnTargetRefreshSpeed);
        SubscribeLocalEvent<NibiruAnimalGrabbedComponent, NibiruAnimalDetachDoAfterEvent>(OnDetachDoAfter);

        // Перехватываем любую попытку взаимодействия с животным-захватчиком,
        // чтобы не дать цели просто нажать "отпустить" как при обычном Pull.
        SubscribeLocalEvent<NibiruAnimalGrabbedComponent, InteractHandEvent>(OnAnimalInteractHand);
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  Update: тик-урон + тряска жертвы
    // ──────────────────────────────────────────────────────────────────────────

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<NibiruAnimalGrabbedComponent>();
        while (query.MoveNext(out var animalUid, out var grabbed))
        {
            if (grabbed.Target == null || !EntityManager.EntityExists(grabbed.Target.Value))
            {
                // Цель пропала — снимаем захват
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

            // ── Тряска (позиционное смещение туда-сюда) ───────────────────
            grabbed.ShakeAccumulator += frameTime;
            if (grabbed.ShakeAccumulator >= grabbed.ShakeInterval)
            {
                grabbed.ShakeAccumulator -= grabbed.ShakeInterval;
                grabbed.ShakeDirection = -grabbed.ShakeDirection;

                if (TryComp<TransformComponent>(animalUid, out var animalXform) &&
                    TryComp<TransformComponent>(target, out var targetXform))
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

                    var offset = perp * grabbed.ShakeAmplitude * grabbed.ShakeDirection;
                    var newWorldPos = targetPos + offset;

                    // Перемещаем цель в мировых координатах
                    var newCoords = new EntityCoordinates(targetXform.ParentUid,
                        Vector2.Transform(newWorldPos, _xform.GetInvWorldMatrix(targetXform.ParentUid)));
                    _xform.SetCoordinates(target, newCoords);
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
        if (component.Target != null && EntityManager.EntityExists(component.Target.Value))
        {
            RemComp<NibiruAnimalGrabbedTargetComponent>(component.Target.Value);
            _movementSpeed.RefreshMovementSpeedModifiers(component.Target.Value);
        }
    }

    private void OnTargetRefreshSpeed(EntityUid uid, NibiruAnimalGrabbedTargetComponent component, RefreshMovementSpeedModifiersEvent args)
    {
        args.ModifySpeed(component.SlowdownMultiplier, component.SlowdownMultiplier);
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  Отцепление только через DoAfter — никакой мгновенной кнопки
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Перехватываем взаимодействие с животным-захватчиком.
    /// Вместо мгновенного отпускания Pull запускаем DoAfter.
    /// </summary>
    private void OnAnimalInteractHand(EntityUid uid, NibiruAnimalGrabbedComponent component, InteractHandEvent args)
    {
        // uid — животное, args.User — тот, кто взаимодействует
        if (component.Target != args.User)
            return;

        // Блокируем стандартное поведение (чтобы не сработал Stop Pull из PullableComponent)
        args.Handled = true;

        // Проверяем, не запущен ли уже DoAfter
        var doAfterArgs = new DoAfterArgs(EntityManager, args.User, component.DetachDuration,
            new NibiruAnimalDetachDoAfterEvent(), uid, uid)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true
        };

        _doAfter.TryStartDoAfter(doAfterArgs);
        _popup.PopupEntity(Loc.GetString("nibiru-animal-grab-detaching"), uid, args.User);
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

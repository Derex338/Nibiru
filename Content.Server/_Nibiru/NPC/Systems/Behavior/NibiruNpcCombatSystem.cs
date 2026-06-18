using Content.Server.Jittering;
using Content.Server.NPC.Systems;
using Content.Shared._Nibiru.NPC.Behavior;
using Content.Shared._Nibiru.NPC.Training;
using Content.Shared.CombatMode;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Jittering;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.NPC.Systems;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Melee;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;
using System.Numerics;

namespace Content.Server._Nibiru.NPC.Systems.Behavior;

public sealed class NibiruNpcCombatSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedMeleeWeaponSystem _melee = default!;
    [Dependency] private readonly SharedCombatModeSystem _combat = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly JitteringSystem _jitter = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly NPCSteeringSystem _steering = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly PullingSystem _pulling = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<NibiruNpcBehaviorComponent, StartCollideEvent>(OnCollide);
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  Entry point from BehaviorSystem
    // ──────────────────────────────────────────────────────────────────────────

    public void ProcessCombat(EntityUid uid, NibiruNpcBehaviorComponent behavior, TransformComponent xform, float frameTime)
    {
        switch (behavior.CurrentState)
        {
            case NibiruNpcState.Charging:
                ProcessCharging(uid, behavior, xform, frameTime);
                break;
            case NibiruNpcState.Attacking:
                ProcessAttacking(uid, behavior, xform, frameTime);
                break;
            case NibiruNpcState.Fleeing:
                ProcessFleeing(uid, behavior);
                break;
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  Default attack (stand and bite)
    // ──────────────────────────────────────────────────────────────────────────

    private void ProcessAttacking(EntityUid uid, NibiruNpcBehaviorComponent behavior, TransformComponent xform, float frameTime)
    {
        if (behavior.CurrentTarget == null || !EntityManager.EntityExists(behavior.CurrentTarget.Value))
        {
            behavior.CurrentTarget = null;
            behavior.CurrentCommand = null;
            behavior.CurrentState = NibiruNpcState.Returning;
            return;
        }

        var target = behavior.CurrentTarget.Value;
        if (_mobState.IsIncapacitated(target) || !TryComp<TransformComponent>(target, out var targetXform))
        {
            behavior.CurrentTarget = null;
            behavior.CurrentCommand = null;
            behavior.CurrentState = NibiruNpcState.Returning;
            return;
        }

        if (!xform.Coordinates.TryDistance(EntityManager, targetXform.Coordinates, out var distance))
        {
            behavior.CurrentTarget = null;
            behavior.CurrentCommand = null;
            behavior.CurrentState = NibiruNpcState.Returning;
            return;
        }

        if (distance > 2f)
        {
            behavior.CurrentState = NibiruNpcState.Chasing;
            return;
        }

        // HitAndRun style: bite → leap backward → wait → repeat
        if (behavior.CombatStyle == NibiruCombatStyle.HitAndRun)
        {
            ProcessHitAndRun(uid, behavior, xform, target, targetXform, frameTime);
            return;
        }

        // Default: stay in combat mode and bite
        if (TryComp<CombatModeComponent>(uid, out var combatMode) && !combatMode.IsInCombatMode)
            _combat.SetInCombatMode(uid, true, combatMode);

        if (_melee.TryGetWeapon(uid, out var weaponUid, out var weapon) && _timing.CurTime >= weapon.NextAttack)
            _melee.AttemptLightAttack(uid, weaponUid, weapon, target);

        _steering.Register(uid, new EntityCoordinates(target, Vector2.Zero));
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  HitAndRun: укусил → отпрыгнул → подождал → снова
    // ──────────────────────────────────────────────────────────────────────────

    private void ProcessHitAndRun(EntityUid uid, NibiruNpcBehaviorComponent behavior,
        TransformComponent xform, EntityUid target, TransformComponent targetXform, float frameTime)
    {
        switch (behavior.HitAndRunPhase)
        {
            // Фаза 0: начальная — входим в боевой режим, делаем укус
            case 0:
            {
                if (TryComp<CombatModeComponent>(uid, out var cm) && !cm.IsInCombatMode)
                    _combat.SetInCombatMode(uid, true, cm);

                if (_melee.TryGetWeapon(uid, out var wUid, out var w) && _timing.CurTime >= w.NextAttack)
                {
                    _melee.AttemptLightAttack(uid, wUid, w, target);

                    // Вычисляем направление прыжка: от цели к нам
                    _steering.Unregister(uid);
                    var myPos = _xform.GetWorldPosition(xform);
                    var targetPos = _xform.GetWorldPosition(targetXform);
                    var away = myPos - targetPos;
                    behavior.LeapDirection = away.LengthSquared() > 0.01f
                        ? Vector2.Normalize(away)
                        : Vector2.UnitX;

                    // Запускаем отпрыжок: время = расстояние / скорость
                    behavior.HitAndRunTimer = behavior.LeapDistance / behavior.LeapSpeed;
                    behavior.HitAndRunPhase = 2; // перейти в фазу прыжка

                    // Придаём импульс назад (с ThrownItem-эффектом высоты не делаем,
                    // но через PhysicsSystem — это похоже на бросок)
                    if (TryComp<PhysicsComponent>(uid, out var phys))
                        _physics.SetLinearVelocity(uid, behavior.LeapDirection * behavior.LeapSpeed, body: phys);
                }
                else
                {
                    // Пока cooldown атаки — стоим и ждём
                    _steering.Register(uid, new EntityCoordinates(target, Vector2.Zero));
                }
                break;
            }

            // Фаза 2: летим назад
            case 2:
            {
                behavior.HitAndRunTimer -= frameTime;

                if (TryComp<PhysicsComponent>(uid, out var phys))
                    _physics.SetLinearVelocity(uid, behavior.LeapDirection * behavior.LeapSpeed, body: phys);

                if (behavior.HitAndRunTimer <= 0f)
                {
                    // Приземлились — гасим скорость
                    if (TryComp<PhysicsComponent>(uid, out var physStop))
                        _physics.SetLinearVelocity(uid, Vector2.Zero, body: physStop);

                    behavior.HitAndRunTimer = behavior.HitAndRunWaitDuration;
                    behavior.HitAndRunPhase = 3; // ждать
                }
                break;
            }

            // Фаза 3: стоим секунду
            case 3:
            {
                behavior.HitAndRunTimer -= frameTime;

                if (behavior.HitAndRunTimer <= 0f)
                {
                    // Готовы к следующей атаке — сбрасываем в фазу 0 и идём к цели
                    behavior.HitAndRunPhase = 0;
                    _steering.Register(uid, new EntityCoordinates(target, Vector2.Zero));
                }
                break;
            }
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  Flee
    // ──────────────────────────────────────────────────────────────────────────

    private void ProcessFleeing(EntityUid uid, NibiruNpcBehaviorComponent behavior)
    {
        if (TryComp<CombatModeComponent>(uid, out var combatMode) && combatMode.IsInCombatMode)
            _combat.SetInCombatMode(uid, false, combatMode);
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  Charge (разбег)
    //
    //  Фазы управляются через CombatTimer:
    //    IsCombatActionActive = false  →  только что перешли в Charging — останавливаемся и прицеливаемся
    //    IsCombatActionActive = true, CombatTimer > 0  →  трясёмся (jitter) ChargeShakeDuration сек
    //    IsCombatActionActive = true, CombatTimer <= 0 →  мчимся строго по линии ChargeMaxDuration сек
    //
    //  ВАЖНО: CombatTimer в этой фазе увеличивается (отрицательный → нуль),
    //  поэтому в BehaviorSystem.Update() его не трогаем.
    // ──────────────────────────────────────────────────────────────────────────

    private void ProcessCharging(EntityUid uid, NibiruNpcBehaviorComponent behavior, TransformComponent xform, float frameTime)
    {
        // ── Шаг 1: инициализация фазы ─────────────────────────────────────────
        if (!behavior.IsCombatActionActive)
        {
            // Полностью останавливаемся
            _steering.Unregister(uid);
            if (TryComp<PhysicsComponent>(uid, out var physInit))
                _physics.SetLinearVelocity(uid, Vector2.Zero, body: physInit);

            // Фиксируем направление на цель
            if (behavior.CurrentTarget != null && TryComp<TransformComponent>(behavior.CurrentTarget.Value, out var targetXform))
            {
                var myPos = _xform.GetWorldPosition(xform);
                var targetPos = _xform.GetWorldPosition(targetXform);
                var dir = targetPos - myPos;
                behavior.ChargeDirection = dir.LengthSquared() > 0.01f ? Vector2.Normalize(dir) : Vector2.UnitX;
                _xform.SetLocalRotation(uid, behavior.ChargeDirection.ToAngle());
            }

            // Запускаем тряску
            _jitter.AddJitter(uid, 10f, 40f);
            behavior.IsCombatActionActive = true;
            behavior.CombatTimer = behavior.ChargeShakeDuration; // положительный — отсчёт тряски
            return;
        }

        // ── Шаг 2: тряска ─────────────────────────────────────────────────────
        if (behavior.CombatTimer > 0)
        {
            // Стоим на месте пока трясёмся
            if (TryComp<PhysicsComponent>(uid, out var physics))
                _physics.SetLinearVelocity(uid, Vector2.Zero, body: physics);

            behavior.CombatTimer -= frameTime;

            if (behavior.CombatTimer <= 0)
            {
                // Тряска закончилась — играем звук и начинаем разбег
                RemComp<JitteringComponent>(uid);
                if (behavior.AggroSound != null)
                    _audio.PlayPvs(behavior.AggroSound, uid);

                // Отрицательный таймер: по модулю = оставшееся время разбега
                behavior.CombatTimer = -behavior.ChargeMaxDuration;
            }
            return;
        }

        // ── Шаг 3: разбег строго по линии ─────────────────────────────────────
        if (behavior.CombatTimer < 0)
        {
            if (TryComp<PhysicsComponent>(uid, out var physics))
                _physics.SetLinearVelocity(uid, behavior.ChargeDirection * behavior.ChargeSpeed, body: physics);

            behavior.CombatTimer += frameTime; // движется к нулю

            if (behavior.CombatTimer >= 0)
            {
                // Разбег закончен — тормозим и переходим в обычное преследование
                behavior.IsCombatActionActive = false;
                behavior.HitAndRunPhase = 0;
                behavior.CurrentState = NibiruNpcState.Chasing;
                // CombatTimer = cooldown перед следующим разбегом — устанавливаем в BehaviorComponent
                behavior.CombatTimer = 5f;

                if (TryComp<PhysicsComponent>(uid, out var phys))
                    _physics.SetLinearVelocity(uid, Vector2.Zero, body: phys);
            }
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  Collision during charge
    // ──────────────────────────────────────────────────────────────────────────

    private void OnCollide(EntityUid uid, NibiruNpcBehaviorComponent component, ref StartCollideEvent args)
    {
        if (component.CurrentState != NibiruNpcState.Charging || !component.IsCombatActionActive)
            return;

        // Только в фазе разбега (CombatTimer < 0)
        if (component.CombatTimer >= 0)
            return;

        var other = args.OtherEntity;
        if (!_mobState.IsAlive(other) || other == uid)
            return;

        if (component.ChargeDamage != null)
            _damageable.TryChangeDamage(other, component.ChargeDamage, true, origin: uid);

        var myPos = _xform.GetWorldPosition(uid);
        var otherPos = _xform.GetWorldPosition(other);
        var dir = otherPos - myPos;
        if (dir != Vector2.Zero)
        {
            dir = Vector2.Normalize(dir);
            _throwing.TryThrow(other, dir * component.ChargeKnockbackForce, 1f, uid);
        }
    }
}

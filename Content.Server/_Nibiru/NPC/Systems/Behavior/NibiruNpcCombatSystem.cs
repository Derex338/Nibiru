using System.Numerics;
using Content.Server._Nibiru.NPC.Systems.Utility;
using Content.Shared._Nibiru.NPC.Behavior;
using Content.Shared.CombatMode;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Systems;
using Content.Shared.Weapons.Melee;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;
using Content.Server.Jittering;
using Content.Shared.Jittering;
using Content.Shared.Movement.Systems;
using Robust.Shared.Physics.Events;
using Content.Shared.Throwing;
using Robust.Shared.Physics.Components;
using Content.Server.NPC.Systems;
using Robust.Shared.Map;

namespace Content.Server._Nibiru.NPC.Systems.Behavior;

/// <summary>
/// Система управления боевым поведением NPC.
/// Обрабатывает атаки, разбеги и тактические маневры.
/// </summary>
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
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<NibiruNpcBehaviorComponent, StartCollideEvent>(OnCollide);
    }

    private void OnCollide(EntityUid uid, NibiruNpcBehaviorComponent component, ref StartCollideEvent args)
    {
        if (component.CurrentState != NibiruNpcState.Charging || !component.IsCombatActionActive)
            return;

        if (component.CombatTimer >= 0) // Еще фаза тряски
            return;

        var other = args.OtherEntity;
        if (!_mobState.IsAlive(other) || other == uid)
            return;

        // Наносим урон
        if (component.ChargeDamage != null)
        {
            _damageable.TryChangeDamage(other, component.ChargeDamage, true, origin: uid);
        }

        // Отбрасывание
        var myPos = _xform.GetWorldPosition(uid);
        var otherPos = _xform.GetWorldPosition(other);
        var dir = otherPos - myPos;
        
        if (dir != Vector2.Zero)
        {
            dir = Vector2.Normalize(dir);
            // Отбрасываем немного в сторону и назад
            _throwing.TryThrow(other, dir * component.ChargeKnockbackForce, 1f, uid);
        }
    }

    public void ProcessCombat(EntityUid uid, NibiruNpcBehaviorComponent behavior, TransformComponent xform, float frameTime)
    {
        switch (behavior.CurrentState)
        {
            case NibiruNpcState.Charging:
                ProcessCharging(uid, behavior, xform, frameTime);
                break;
            case NibiruNpcState.Attacking:
                ProcessAttacking(uid, behavior, xform);
                break;
            case NibiruNpcState.Fleeing:
                ProcessFleeing(uid, behavior, xform);
                break;
        }
    }

    private void ProcessCharging(EntityUid uid, NibiruNpcBehaviorComponent behavior, TransformComponent xform, float frameTime)
    {
        // Фаза 1: Замирание и тряска
        if (!behavior.IsCombatActionActive)
        {
            _steering.Unregister(uid);
            
            // Животное должно остановиться на месте
            if (TryComp<PhysicsComponent>(uid, out var physInit))
                _physics.SetLinearVelocity(uid, Vector2.Zero, body: physInit);

            _jitter.AddJitter(uid, 10f, 40f); // Интенсивная тряска
            behavior.IsCombatActionActive = true;
            behavior.CombatTimer = 1.0f; // строго секунду трясется
            
            // Выбираем направление в сторону цели для разбега
            if (behavior.CurrentTarget != null && TryComp<TransformComponent>(behavior.CurrentTarget.Value, out var targetXform))
            {
                var myPos = _xform.GetWorldPosition(xform);
                var targetPos = _xform.GetWorldPosition(targetXform);
                var dir = targetPos - myPos;
                if (dir.LengthSquared() > 0.01f)
                {
                    behavior.ChargeDirection = Vector2.Normalize(dir);
                    _xform.SetLocalRotation(uid, behavior.ChargeDirection.ToAngle());
                }
                else
                    behavior.ChargeDirection = Vector2.UnitX;
            }
            return;
        }

        // Ждем пока закончится тряска (ровно 1 секунда)
        if (behavior.CombatTimer > 0)
        {
            // Пока трясемся - стоим на месте
            if (TryComp<PhysicsComponent>(uid, out var physics))
                _physics.SetLinearVelocity(uid, Vector2.Zero, body: physics);
            
            behavior.CombatTimer -= frameTime;
            if (behavior.CombatTimer <= 0)
            {
                // Начинаем разбег
                RemComp<JitteringComponent>(uid);
                
                if (behavior.AggroSound != null)
                    _audio.PlayPvs(behavior.AggroSound, uid);
                
                // Разбег ровно на 5 тайлов. Время = дистанция / скорость
                float chargeDistance = 5f;
                float duration = chargeDistance / behavior.ChargeSpeed;
                behavior.CombatTimer = -duration;
            }
            return;
        }

        // Фаза 2: Движение строго по прямой
        if (behavior.CombatTimer < 0)
        {
            if (TryComp<PhysicsComponent>(uid, out var physics))
            {
                // Задаем строгую линейную скорость в направлении разбега
                _physics.SetLinearVelocity(uid, behavior.ChargeDirection * behavior.ChargeSpeed, body: physics);
            }

            behavior.CombatTimer += frameTime;
            if (behavior.CombatTimer >= 0)
            {
                // Конец разбега
                behavior.IsCombatActionActive = false;
                behavior.CurrentState = NibiruNpcState.Chasing;
                behavior.CombatTimer = 5f; // Кулдаун
                
                if (TryComp<PhysicsComponent>(uid, out var phys))
                    _physics.SetLinearVelocity(uid, Vector2.Zero, body: phys);
            }
        }
    }

    private void ProcessAttacking(EntityUid uid, NibiruNpcBehaviorComponent behavior, TransformComponent xform)
    {
        if (behavior.CurrentTarget == null || !EntityManager.EntityExists(behavior.CurrentTarget.Value))
        {
            behavior.CurrentState = NibiruNpcState.Returning;
            behavior.CurrentTarget = null;
            return;
        }

        var target = behavior.CurrentTarget.Value;
        if (_mobState.IsIncapacitated(target))
        {
            behavior.CurrentState = NibiruNpcState.Returning;
            behavior.CurrentTarget = null;
            return;
        }

        if (!TryComp<TransformComponent>(target, out var targetXform))
        {
            behavior.CurrentState = NibiruNpcState.Returning;
            behavior.CurrentTarget = null;
            return;
        }

        if (!xform.Coordinates.TryDistance(EntityManager, targetXform.Coordinates, out var distance))
        {
            behavior.CurrentState = NibiruNpcState.Returning;
            behavior.CurrentTarget = null;
            return;
        }

        if (distance > 2f)
        {
            behavior.CurrentState = NibiruNpcState.Chasing;
            return;
        }

        if (TryComp<CombatModeComponent>(uid, out var combatMode) && !combatMode.IsInCombatMode)
            _combat.SetInCombatMode(uid, true, combatMode);

        if (_melee.TryGetWeapon(uid, out var weaponUid, out var weapon))
        {
            if (_timing.CurTime >= weapon.NextAttack)
            {
                if (_melee.AttemptLightAttack(uid, weaponUid, weapon, target))
                {
                    // Логика Hit and Run
                    if (behavior.CombatStyle == NibiruCombatStyle.HitAndRun)
                    {
                        behavior.IsCombatActionActive = true;
                        behavior.CombatTimer = 1.2f;
                        behavior.CurrentState = NibiruNpcState.Fleeing;
                        return;
                    }
                }
            }
        }

        _steering.Register(uid, new EntityCoordinates(target, Vector2.Zero));
    }

    private void ProcessFleeing(EntityUid uid, NibiruNpcBehaviorComponent behavior, TransformComponent xform)
    {
        if (TryComp<CombatModeComponent>(uid, out var combatMode) && combatMode.IsInCombatMode)
            _combat.SetInCombatMode(uid, false, combatMode);

        // Если это тактический отскок
        if (behavior.IsCombatActionActive && behavior.CombatStyle == NibiruCombatStyle.HitAndRun)
        {
            if (behavior.CombatTimer <= 0)
            {
                behavior.IsCombatActionActive = false;
                behavior.CurrentState = NibiruNpcState.Chasing;
                return;
            }
        }
        
        // В NibiruNpcBehaviorSystem останется общая логика бегства от угрозы, 
        // здесь только боевые аспекты (таймеры).
    }
}

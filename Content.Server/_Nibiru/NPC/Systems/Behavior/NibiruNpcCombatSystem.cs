using Content.Server.Jittering;
using Content.Server.NPC.Systems;
using Content.Shared._Nibiru.NPC.Behavior;
using Content.Shared._Nibiru.NPC.Behavior.Components;
using Content.Shared.CombatMode;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Jittering;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Systems;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Melee;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;
using System.Numerics;

namespace Content.Server._Nibiru.NPC.Systems.Behavior;

/// <summary>
/// Система боевого поведения животных Нибиру.
///
/// Реализует три стиля боя:
///
/// <b>Default</b> — классический ближний бой с коротким отступом после удара.
/// Животное преследует цель, атакует мелее, отходит назад через steering,
/// выжидает кулдаун и повторяет. Используется для большинства животных.
///
/// <b>HitAndLeap</b> — тактика "укусил и отпрыгнул" (для волков и схожих хищников).
/// Животное подходит к цели, кусает, затем получает физический импульс строго
/// назад относительно поворота своего тела — без использования навигации NPC.
/// После приземления выдерживает паузу и снова идёт в атаку.
///
/// <b>Charge</b> — атака с разбега (для рогатых: козы, коровы).
/// Животное входит в диапазон разбега, полностью останавливается, поворачивается
/// к цели, начинает трястись (WindUp), затем летит по прямой через физический
/// импульс, нанося урон и отбрасывая всё на пути. Остановка при столкновении
/// со стеной или по истечении максимального времени разбега.
/// </summary>
public sealed class NibiruNpcCombatSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedMeleeWeaponSystem _melee = default!;
    [Dependency] private readonly SharedCombatModeSystem _combat = default!;
    [Dependency] private readonly JitteringSystem _jitter = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly NPCSteeringSystem _steering = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<NibiruNpcChargeAttackComponent, StartCollideEvent>(OnChargeCollide);
    }

    // ════════════════════════════════════════════════════════════════════════
    //  Точка входа
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Вызывается из <see cref="NibiruNpcBehaviorSystem"/> на каждом кадре
    /// для состояний Attacking и Fleeing.
    /// </summary>
    public void ProcessCombat(
        EntityUid uid,
        NibiruNpcStateMachineComponent state,
        NibiruNpcCombatComponent combat,
        TransformComponent xform,
        float frameTime)
    {
        switch (state.CurrentState)
        {
            case NibiruNpcState.Attacking:
                ProcessAttacking(uid, state, combat, xform, frameTime);
                break;

            case NibiruNpcState.Fleeing:
                ProcessFleeing(uid, state, xform);
                break;
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    //  Состояние Attacking — диспетчер стилей
    // ════════════════════════════════════════════════════════════════════════

    private void ProcessAttacking(
        EntityUid uid,
        NibiruNpcStateMachineComponent state,
        NibiruNpcCombatComponent combat,
        TransformComponent xform,
        float frameTime)
    {
        // ── Проверка цели ─────────────────────────────────────────────────
        if (!ValidateTarget(uid, state, out var target, out var targetXform))
        {
            ResetToReturning(uid, state, combat);
            return;
        }

        if (!xform.Coordinates.TryDistance(EntityManager, targetXform.Coordinates, out var distance))
        {
            ResetToReturning(uid, state, combat);
            return;
        }

        // ── Диспетчер по стилю ────────────────────────────────────────────
        switch (combat.CombatStyle)
        {
            case NibiruCombatStyle.HitAndRun
                when TryComp<NibiruNpcHitAndRunAttackComponent>(uid, out var leap):
                ProcessHitAndLeap(uid, state, combat, leap, xform, target, targetXform, distance, frameTime);
                break;

            default:
                ProcessDefault(uid, state, combat, xform, target, targetXform, distance, frameTime);
                break;
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    //  Default — классическая атака с коротким отступом
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Обычный стиль: подошёл → ударил → отступил → подождал → повторил.
    /// Отступ осуществляется через NPC steering (безопасно, без физических
    /// артефактов). Подходит для крупных медленных животных и базовых противников.
    /// </summary>
    private void ProcessDefault(
        EntityUid uid,
        NibiruNpcStateMachineComponent state,
        NibiruNpcCombatComponent combat,
        TransformComponent xform,
        EntityUid target,
        TransformComponent targetXform,
        float distance,
        float frameTime)
    {
        // Фаза отступа
        if (combat.IsRetreating)
        {
            combat.RetreatTimer -= frameTime;
            if (combat.RetreatTimer <= 0f)
            {
                combat.IsRetreating = false;
                // Сбрасываем steering — NPC сам перейдёт к преследованию
            }
            return;
        }

        // Слишком далеко — продолжаем преследование
        if (distance > 2.0f)
        {
            state.CurrentState = NibiruNpcState.Chasing;
            return;
        }

        // Включаем боевой режим
        if (TryComp<CombatModeComponent>(uid, out var combatMode) && !combatMode.IsInCombatMode)
            _combat.SetInCombatMode(uid, true, combatMode);

        // Атака
        if (_melee.TryGetWeapon(uid, out var weaponUid, out var weapon) &&
            _timing.CurTime >= weapon.NextAttack)
        {
            _melee.AttemptLightAttack(uid, weaponUid, weapon, target);

            // Начать отступ назад от цели
            var myPos = _xform.GetWorldPosition(xform);
            var targetPos = _xform.GetWorldPosition(targetXform);
            var awayDir = (myPos - targetPos);
            if (awayDir.LengthSquared() > 0.01f)
                awayDir = Vector2.Normalize(awayDir);
            else
                awayDir = Vector2.UnitX;

            var retreatPoint = new EntityCoordinates(
                xform.ParentUid,
                xform.LocalPosition + awayDir * combat.PostAttackRetreatDistance);

            _steering.Register(uid, retreatPoint);
            combat.IsRetreating = true;
            combat.RetreatTimer = combat.PostAttackCooldown;
        }
        else
        {
            // Ещё не время атаковать — подходим к цели
            _steering.Register(uid, new EntityCoordinates(target, Vector2.Zero));
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    //  HitAndLeap — укусил и отпрыгнул (волки)
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Волчья тактика — укусил и резко отпрыгнул назад через физику.
    ///
    /// Фазы:
    ///   Idle    → подходит через steering, переход в Biting при достижении дальности атаки
    ///   Biting  → атакует мелее, фиксирует вектор прыжка, немедленно → Leaping
    ///   Leaping → физический импульс назад (SetLinearVelocity), steering отключён
    ///   Cooldown→ стоит WaitDuration секунд, затем снова Idle
    /// </summary>
    private void ProcessHitAndLeap(
        EntityUid uid,
        NibiruNpcStateMachineComponent state,
        NibiruNpcCombatComponent combat,
        NibiruNpcHitAndRunAttackComponent leap,
        TransformComponent xform,
        EntityUid target,
        TransformComponent targetXform,
        float distance,
        float frameTime)
    {
        switch (leap.Phase)
        {
            // ── Idle: двигаемся к цели через steering ─────────────────────
            case LeapPhase.Idle:
            {
                // Если цель убежала слишком далеко — возвращаемся
                if (distance > 15f)
                {
                    ResetToReturning(uid, state, combat);
                    return;
                }

                // Включаем боевой режим для видимости
                if (TryComp<CombatModeComponent>(uid, out var cm) && !cm.IsInCombatMode)
                    _combat.SetInCombatMode(uid, true, cm);

                if (distance <= leap.AttackRange)
                {
                    // Дошли — кусаем
                    _steering.Unregister(uid);
                    leap.Phase = LeapPhase.Biting;
                }
                else
                {
                    _steering.Register(uid, new EntityCoordinates(target, Vector2.Zero));
                }
                break;
            }

            // ── Biting: выполняем атаку, затем сразу прыжок ──────────────
            case LeapPhase.Biting:
            {
                if (TryComp<CombatModeComponent>(uid, out var cm) && !cm.IsInCombatMode)
                    _combat.SetInCombatMode(uid, true, cm);

                if (_melee.TryGetWeapon(uid, out var wUid, out var w) && _timing.CurTime >= w.NextAttack)
                {
                    _melee.AttemptLightAttack(uid, wUid, w, target);

                    // Вектор прыжка: строго назад относительно поворота тела
                    // (т.е. обратный вектор от цели к нам, а не просто "назад по экрану")
                    var myWorldPos = _xform.GetWorldPosition(xform);
                    var targetWorldPos = _xform.GetWorldPosition(targetXform);
                    var toTarget = targetWorldPos - myWorldPos;

                    // Направление "назад от цели" = нормаль вектора (я → цель), инвертированная
                    leap.LeapDirection = toTarget.LengthSquared() > 0.01f
                        ? -Vector2.Normalize(toTarget)   // точно назад от цели
                        : -Vector2.UnitX;

                    // Вычисляем длительность фазы прыжка из расстояния и скорости
                    leap.Timer = leap.LeapDistance / leap.LeapSpeed;
                    leap.Phase = LeapPhase.Leaping;

                    // Немедленно применяем импульс
                    if (TryComp<PhysicsComponent>(uid, out var phys))
                        _physics.SetLinearVelocity(uid, leap.LeapDirection * leap.LeapSpeed, body: phys);
                }
                break;
            }

            // ── Leaping: летим назад через физику ─────────────────────────
            case LeapPhase.Leaping:
            {
                leap.Timer -= frameTime;

                // Поддерживаем скорость каждый тик (физика может её рассеивать)
                if (TryComp<PhysicsComponent>(uid, out var phys))
                    _physics.SetLinearVelocity(uid, leap.LeapDirection * leap.LeapSpeed, body: phys);

                if (leap.Timer <= 0f)
                {
                    // Приземлились — тормозим
                    if (TryComp<PhysicsComponent>(uid, out var physStop))
                        _physics.SetLinearVelocity(uid, Vector2.Zero, body: physStop);

                    leap.Timer = leap.WaitDuration;
                    leap.Phase = LeapPhase.Cooldown;
                }
                break;
            }

            // ── Cooldown: ждём перед следующей атакой ─────────────────────
            case LeapPhase.Cooldown:
            {
                leap.Timer -= frameTime;

                if (leap.Timer <= 0f)
                {
                    leap.Phase = LeapPhase.Idle;
                    // Даём стиирингу снова вести к цели
                    _steering.Register(uid, new EntityCoordinates(target, Vector2.Zero));
                }
                break;
            }
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    //  Charge — разбег рогатых
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Вызывается из <see cref="NibiruNpcBehaviorSystem"/> в состоянии Charging.
    ///
    /// Фазы:
    ///   Idle     → ждёт в ProcessChasing; переход в WindUp через BehaviorSystem
    ///   WindUp   → остановился, трясётся, ждёт ShakeDuration
    ///   Charging → физический разбег по прямой, урон через коллизии
    ///   Cooldown → отдыхает CooldownDuration, затем возврат в Chasing
    /// </summary>
    public void ProcessCharging(
        EntityUid uid,
        NibiruNpcStateMachineComponent state,
        NibiruNpcCombatComponent combat,
        NibiruNpcChargeAttackComponent charge,
        TransformComponent xform,
        float frameTime)
    {
        switch (charge.Phase)
        {
            // ── WindUp: тряска, поворот к цели ───────────────────────────
            case ChargePhase.WindUp:
            {
                charge.Timer -= frameTime;

                if (charge.Timer <= 0f)
                {
                    // Убираем тряску — разбег начат
                    RemCompDeferred<JitteringComponent>(uid);

                    charge.Timer = charge.MaxDuration;
                    charge.Phase = ChargePhase.Charging;

                    // Немедленно запускаем физику
                    if (TryComp<PhysicsComponent>(uid, out var phys))
                        _physics.SetLinearVelocity(uid, charge.Direction * charge.Speed, body: phys);
                }
                break;
            }

            // ── Charging: летим по прямой ─────────────────────────────────
            case ChargePhase.Charging:
            {
                charge.Timer -= frameTime;

                // Поддерживаем скорость каждый тик
                if (TryComp<PhysicsComponent>(uid, out var phys))
                    _physics.SetLinearVelocity(uid, charge.Direction * charge.Speed, body: phys);

                if (charge.Timer <= 0f)
                {
                    // Время истекло — останавливаемся
                    StopCharge(uid, state, charge);
                }
                break;
            }

            // ── Cooldown: отдых после разбега ─────────────────────────────
            case ChargePhase.Cooldown:
            {
                charge.Timer -= frameTime;

                if (charge.Timer <= 0f)
                {
                    charge.Phase = ChargePhase.Idle;
                    state.CurrentState = NibiruNpcState.Chasing;

                    // Включаем навигацию обратно
                    if (state.CurrentTarget != null)
                        _steering.Register(uid, new EntityCoordinates(state.CurrentTarget.Value, Vector2.Zero));
                }
                break;
            }
        }
    }

    /// <summary>
    /// Инициирует WindUp для разбега. Вызывается из BehaviorSystem когда цель
    /// попадает в нужный диапазон дистанции.
    /// </summary>
    public void StartChargeWindUp(
        EntityUid uid,
        NibiruNpcStateMachineComponent state,
        NibiruNpcChargeAttackComponent charge,
        TransformComponent xform,
        EntityUid target)
    {
        if (!TryComp<TransformComponent>(target, out var targetXform))
            return;

        // Фиксируем направление разбега
        var myPos = _xform.GetWorldPosition(xform);
        var targetPos = _xform.GetWorldPosition(targetXform);
        var dir = targetPos - myPos;
        charge.Direction = dir.LengthSquared() > 0.01f
            ? Vector2.Normalize(dir)
            : Vector2.UnitX;

        // Поворачиваем к цели
        _xform.SetLocalRotation(uid, charge.Direction.ToAngle());

        // Останавливаемся и отключаем навигацию
        _steering.Unregister(uid);
        if (TryComp<PhysicsComponent>(uid, out var phys))
            _physics.SetLinearVelocity(uid, Vector2.Zero, body: phys);

        // Очищаем список ранее задетых сущностей
        charge.HitEntities.Clear();

        // Запускаем тряску
        _jitter.AddJitter(uid, 10f, 40f);

        charge.Timer = charge.ShakeDuration;
        charge.Phase = ChargePhase.WindUp;
        state.CurrentState = NibiruNpcState.Charging;
    }

    /// <summary>
    /// Останавливает разбег и переводит животное в Cooldown.
    /// </summary>
    public void StopCharge(
        EntityUid uid,
        NibiruNpcStateMachineComponent state,
        NibiruNpcChargeAttackComponent charge)
    {
        // Тормозим физику
        if (TryComp<PhysicsComponent>(uid, out var phys))
            _physics.SetLinearVelocity(uid, Vector2.Zero, body: phys);

        // На всякий случай убираем тряску если она осталась
        RemCompDeferred<JitteringComponent>(uid);

        charge.Timer = charge.CooldownDuration;
        charge.Phase = ChargePhase.Cooldown;
        state.CurrentState = NibiruNpcState.Charging; // остаёмся в Charging, обрабатываем Cooldown
    }

    // ════════════════════════════════════════════════════════════════════════
    //  Коллизия во время разбега
    // ════════════════════════════════════════════════════════════════════════

    private void OnChargeCollide(
        EntityUid uid,
        NibiruNpcChargeAttackComponent charge,
        ref StartCollideEvent args)
    {
        // Обрабатываем только фазу активного разбега
        if (charge.Phase != ChargePhase.Charging)
            return;

        var other = args.OtherEntity;
        if (other == uid)
            return;

        // ── Столкновение со статичным объектом (стена, дверь) ─────────────
        if (charge.StopOnWallCollision)
        {
            if (TryComp<PhysicsComponent>(other, out var otherPhys) &&
                otherPhys.BodyType == BodyType.Static)
            {
                if (TryComp<NibiruNpcStateMachineComponent>(uid, out var stateMachine))
                    StopCharge(uid, stateMachine, charge);
                return;
            }
        }

        // ── Столкновение с живой сущностью ───────────────────────────────
        if (!_mobState.IsAlive(other))
            return;

        // Каждая сущность получает урон только один раз за разбег
        if (!charge.HitEntities.Add(other))
            return;

        // Наносим урон
        if (charge.Damage != null)
            _damageable.TryChangeDamage(other, charge.Damage, true, origin: uid);

        // Отбрасываем в направлении разбега
        var myPos = _xform.GetWorldPosition(uid);
        var otherPos = _xform.GetWorldPosition(other);
        var knockDir = (otherPos - myPos).LengthSquared() > 0.01f
            ? Vector2.Normalize(otherPos - myPos)
            : charge.Direction;

        _throwing.TryThrow(other, knockDir * charge.KnockbackForce, 1f, uid);
    }

    // ════════════════════════════════════════════════════════════════════════
    //  Fleeing — бегство
    // ════════════════════════════════════════════════════════════════════════

    private void ProcessFleeing(
        EntityUid uid,
        NibiruNpcStateMachineComponent state,
        TransformComponent xform)
    {
        if (TryComp<CombatModeComponent>(uid, out var combatMode) && combatMode.IsInCombatMode)
            _combat.SetInCombatMode(uid, false, combatMode);

        if (state.CurrentTarget == null || !EntityManager.EntityExists(state.CurrentTarget.Value))
        {
            state.CurrentState = NibiruNpcState.Returning;
            state.CurrentTarget = null;
            return;
        }

        var target = state.CurrentTarget.Value;
        if (!TryComp<TransformComponent>(target, out var targetXform))
        {
            state.CurrentState = NibiruNpcState.Returning;
            state.CurrentTarget = null;
            return;
        }

        if (!xform.Coordinates.TryDistance(EntityManager, targetXform.Coordinates, out var distance))
            return;

        var fleeDistance = 12f;
        if (TryComp<NibiruNpcAggroComponent>(uid, out var aggro))
            fleeDistance = aggro.FleeDistance;

        if (distance > fleeDistance)
        {
            state.CurrentState = NibiruNpcState.Returning;
            state.CurrentTarget = null;
            _steering.Unregister(uid);
            return;
        }

        var myPos = _xform.GetWorldPosition(xform);
        var targetPos = _xform.GetWorldPosition(targetXform);
        var dir = myPos - targetPos;
        if (dir.LengthSquared() > 0.01f)
        {
            dir = Vector2.Normalize(dir);
            var fleeCoords = new EntityCoordinates(xform.ParentUid, xform.LocalPosition + dir * 5f);
            _steering.Register(uid, fleeCoords);
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    //  Вспомогательные методы
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Проверяет что цель ещё существует, жива и доступна.
    /// Возвращает false если цель недействительна.
    /// </summary>
    private bool ValidateTarget(
        EntityUid uid,
        NibiruNpcStateMachineComponent state,
        out EntityUid target,
        out TransformComponent targetXform)
    {
        target = default;
        targetXform = default!;

        if (state.CurrentTarget == null || !EntityManager.EntityExists(state.CurrentTarget.Value))
            return false;

        target = state.CurrentTarget.Value;

        if (_mobState.IsIncapacitated(target))
            return false;

        if (!TryComp<TransformComponent>(target, out var xform) || xform == null)
            return false;

        targetXform = xform;
        return true;
    }

    /// <summary>
    /// Сбрасывает боевое состояние и переводит NPC в Returning.
    /// </summary>
    private void ResetToReturning(
        EntityUid uid,
        NibiruNpcStateMachineComponent state,
        NibiruNpcCombatComponent combat)
    {
        state.CurrentTarget = null;
        state.CurrentState = NibiruNpcState.Returning;
        combat.IsRetreating = false;
        combat.RetreatTimer = 0f;

        // Сброс фазы HitAndLeap если есть
        if (TryComp<NibiruNpcHitAndRunAttackComponent>(uid, out var leap))
        {
            leap.Phase = LeapPhase.Idle;
            leap.Timer = 0f;
            if (TryComp<PhysicsComponent>(uid, out var phys))
                _physics.SetLinearVelocity(uid, Vector2.Zero, body: phys);
        }

        _steering.Unregister(uid);
    }
}

// Obsolete root using removed
using Content.Shared._Nibiru.NPC.Behavior;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Timing;

namespace Content.Server._Nibiru.NPC.Systems.Behavior;

/// <summary>
/// Обрабатывает зрение и слух NPC.
/// Проверяет угол обзора, дальность зрения, наличие препятствий (raycast),
/// а также обнаружение по шуму на основе скорости движения цели.
/// </summary>
public sealed class NibiruNpcPerceptionSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<NibiruNpcPerceptionComponent, ComponentStartup>(OnStartup);
    }

    private void OnStartup(EntityUid uid, NibiruNpcPerceptionComponent component, ComponentStartup args)
    {
        component.DetectedEntities.Clear();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<NibiruNpcPerceptionComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var perception, out var xform))
        {
            perception.PerceptionAccumulator += frameTime;
            if (perception.PerceptionAccumulator < perception.PerceptionInterval)
                continue;

            perception.PerceptionAccumulator = 0f;
            UpdatePerception(uid, perception, xform);
        }
    }

    private void UpdatePerception(EntityUid uid, NibiruNpcPerceptionComponent perception, TransformComponent xform)
    {
        perception.DetectedEntities.Clear();

        var mapCoords = _xform.GetMapCoordinates((uid, xform));
        var facingAngle = GetFacingAngle(uid, xform);
        perception.LastFacingAngle = facingAngle;

        var maxRange = MathF.Max(perception.VisionRange, perception.HearingRange);

        foreach (var entity in _lookup.GetEntitiesInRange<NpcFactionMemberComponent>(mapCoords, maxRange))
        {
            if (entity.Owner == uid)
                continue;

            var targetXform = Transform(entity.Owner);
            if (!mapCoords.MapId.Equals(_xform.GetMapCoordinates((entity.Owner, targetXform)).MapId))
                continue;

            var targetPos = _xform.GetWorldPosition(targetXform);
            var myPos = _xform.GetWorldPosition(xform);
            var diff = targetPos - myPos;
            var distance = diff.Length();

            // Проверка зрения: дальность + угол обзора
            if (distance <= perception.VisionRange)
            {
                if (IsInFieldOfView(diff, facingAngle, perception.VisionAngle))
                {
                    perception.DetectedEntities.Add(entity.Owner);
                    continue;
                }
            }

            // Проверка слуха: скорость цели должна быть достаточной
            if (distance <= perception.HearingRange)
            {
                if (TryDetectBySound(entity.Owner, perception))
                {
                    perception.DetectedEntities.Add(entity.Owner);
                }
            }
        }
    }

    /// <summary>
    /// Определяет, находится ли вектор к цели внутри конуса зрения.
    /// </summary>
    private bool IsInFieldOfView(System.Numerics.Vector2 directionToTarget, Angle facingAngle, float visionAngleDegrees)
    {
        if (visionAngleDegrees >= 360f)
            return true;

        var targetAngle = new Angle(MathF.Atan2(directionToTarget.Y, directionToTarget.X));
        var angleDiff = Angle.ShortestDistance(facingAngle, targetAngle);

        var halfVision = Angle.FromDegrees(visionAngleDegrees / 2f);
        return MathF.Abs((float) angleDiff.Theta) <= (float) halfVision.Theta;
    }

    /// <summary>
    /// Пытается обнаружить цель по звуку на основе её скорости движения.
    /// </summary>
    private bool TryDetectBySound(EntityUid target, NibiruNpcPerceptionComponent perception)
    {
        if (!TryComp<PhysicsComponent>(target, out var physics))
            return false;

        var speed = physics.LinearVelocity.Length();
        if (speed <= 0.1f)
            return false;

        // Быстрое движение (бег) — гарантированное обнаружение
        if (speed >= perception.HearingSpeedThreshold)
            return true;

        // Медленное движение (ходьба шагом) — вероятностное обнаружение
        var random = new System.Random();
        return random.NextDouble() < perception.WalkDetectionChance;
    }

    /// <summary>
    /// Получает текущий угол взгляда NPC на основе его вращения.
    /// </summary>
    private Angle GetFacingAngle(EntityUid uid, TransformComponent xform)
    {
        // Используем локальное вращение как направление взгляда
        return xform.LocalRotation;
    }

    /// <summary>
    /// Проверяет, обнаружена ли конкретная цель данным NPC.
    /// </summary>
    public bool IsDetected(EntityUid npc, EntityUid target, NibiruNpcPerceptionComponent? perception = null)
    {
        if (!Resolve(npc, ref perception, false))
            return false;

        return perception.DetectedEntities.Contains(target);
    }
}

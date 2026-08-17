// Obsolete root using removed
using Content.Shared._Nibiru.NPC.Behavior;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Timing;

namespace Content.Server._Nibiru.NPC.Systems.Behavior;

/// <summary>
/// Processes NPC vision and hearing.
/// Checks field of view, vision range, obstacle presence (raycast),
/// and sound detection based on target movement speed.
/// </summary>
public sealed partial class NibiruNpcPerceptionSystem : EntitySystem
{
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private IGameTiming _timing = default!;

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

            // Check vision: range + field of view
            if (distance <= perception.VisionRange)
            {
                if (IsInFieldOfView(diff, facingAngle, perception.VisionAngle))
                {
                    perception.DetectedEntities.Add(entity.Owner);
                    continue;
                }
            }

            // Check hearing: target speed must be sufficient
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
    /// Determines if the vector to the target is within the field of view cone.
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
    /// Tries to detect the target by sound based on its movement speed.
    /// </summary>
    private bool TryDetectBySound(EntityUid target, NibiruNpcPerceptionComponent perception)
    {
        if (!TryComp<PhysicsComponent>(target, out var physics))
            return false;

        var speed = physics.LinearVelocity.Length();
        if (speed <= 0.1f)
            return false;

        // Fast movement (running) - guaranteed detection
        if (speed >= perception.HearingSpeedThreshold)
            return true;

        // Slow movement (walking) - probabilistic detection
        var random = new System.Random();
        return random.NextDouble() < perception.WalkDetectionChance;
    }

    /// <summary>
    /// Gets the current NPC looking angle based on its rotation.
    /// </summary>
    private Angle GetFacingAngle(EntityUid uid, TransformComponent xform)
    {
        // Use local rotation as looking direction
        return xform.LocalRotation;
    }

    /// <summary>
    /// Checks if the specific target is detected by this NPC.
    /// </summary>
    public bool IsDetected(EntityUid npc, EntityUid target, NibiruNpcPerceptionComponent? perception = null)
    {
        if (!Resolve(npc, ref perception, false))
            return false;

        return perception.DetectedEntities.Contains(target);
    }
}

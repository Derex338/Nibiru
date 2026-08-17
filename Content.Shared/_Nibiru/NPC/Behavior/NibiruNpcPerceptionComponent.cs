using Robust.Shared.GameStates;

namespace Content.Shared._Nibiru.NPC.Behavior;

/// <summary>
/// NPC perception component: vision with configurable field of view angle and hearing.
/// Allows NPC to react only to those who are in the field of view or make noise.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NibiruNpcPerceptionComponent : Component
{
    #region Vision

    /// <summary>
    /// Vision range in tiles.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public float VisionRange = 10f;

    /// <summary>
    /// Vision angle in degrees (full cone, from center in both directions).
    /// 360 = sees in all directions, 120 = standard predator vision,
    /// 270 = wide herbivore vision.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public float VisionAngle = 120f;

    /// <summary>
    /// Perception check interval in seconds.
    /// No need to check every tick — this saves performance.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float PerceptionInterval = 0.5f;

    /// <summary>
    /// Timer until next check.
    /// </summary>
    [ViewVariables]
    public float PerceptionAccumulator;

    #endregion

    #region Hearing

    /// <summary>
    /// Hearing range. Within this radius the NPC can "hear" running or noisy entities.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public float HearingRange = 8f;

    /// <summary>
    /// Minimum movement speed of the target at which the NPC will hear it.
    /// Walking (~1.5) will be below the threshold, running (~4.5) — above.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float HearingSpeedThreshold = 3.0f;

    /// <summary>
    /// Probability multiplier for hearing a walking target (when speed is below the running threshold).
    /// 0.1 = 10% chance each check tick.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float WalkDetectionChance = 0.1f;

    #endregion

    /// <summary>
    /// List of detected targets (visible or heard).
    /// Updated every perception tick.
    /// </summary>
    [ViewVariables]
    public HashSet<EntityUid> DetectedEntities = new();

    /// <summary>
    /// Last known look direction of the NPC (normalized vector).
    /// Used to check the field of view angle.
    /// </summary>
    [ViewVariables]
    public Angle LastFacingAngle;
}

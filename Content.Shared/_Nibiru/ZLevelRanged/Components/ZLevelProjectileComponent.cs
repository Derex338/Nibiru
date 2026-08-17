using Robust.Shared.GameStates;
using Robust.Shared.Map;
using System.Numerics;

namespace Content.Shared._Nibiru.ZLevelRanged.Components;

/// <summary>
/// Allows the projectile to cross Z-levels during flight.
/// At 70% of the path, it checks for the presence of a tile below and falls if it's missing.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ZLevelProjectileComponent : Component
{
    /// <summary>
    /// Can the projectile fly through empty tiles downwards
    /// </summary>
    [DataField]
    public bool CanFallThrough = true;

    /// <summary>
    /// The percentage of the path on which the projectile checks for falling down (0.7 = 70%)
    /// </summary>
    [DataField]
    public float FallCheckDistance = 0.7f;

    /// <summary>
    /// Starting position of the projectile (world)
    /// </summary>
    public Vector2? StartPosition;

    /// <summary>
    /// Initial speed of the projectile (for calculating distance traveled)
    /// </summary>
    public float InitialSpeed;

    /// <summary>
    /// Already checked for falling?
    /// </summary>
    public bool FallChecked = false;

    /// <summary>
    /// Can shoot directly between levels (ignores obstacles between Z-levels)
    /// </summary>
    [DataField]
    public bool DirectFire = false;

    /// <summary>
    /// Initial MapId for tracking level changes
    /// </summary>
    public MapId? OriginalMapId;

    /// <summary>
    /// Time since projectile creation (for accurate 70% path check)
    /// </summary>
    public float TimeAlive;

    /// <summary>
    /// Estimated time of projectile flight to target (calculated at creation)
    /// </summary>
    public float EstimatedFlightTime = 1.0f;
}

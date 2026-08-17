using Robust.Shared.GameStates;

namespace Content.Shared._Nibiru.ZLevelRanged.Components;

/// <summary>
/// Marks the weapon as capable of shooting between Z-levels.
/// Projectiles of this weapon will receive ZLevelProjectileComponent.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ZLevelCapableWeaponComponent : Component
{
    /// <summary>
    /// Can this weapon shoot directly between levels
    /// (the arrow flies in a straight line and can hit another level through an opening)
    /// </summary>
    [DataField]
    public bool AllowDirectFire = true;

    /// <summary>
    /// The percentage of the path on which the projectile checks for falling down
    /// </summary>
    [DataField]
    public float FallCheckDistance = 0.7f;
}

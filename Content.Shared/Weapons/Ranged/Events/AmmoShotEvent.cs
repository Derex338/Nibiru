namespace Content.Shared.Weapons.Ranged.Events;

/// <summary>
/// Raised on a gun when projectiles have been fired from it.
/// </summary>
public sealed class AmmoShotEvent : EntityEventArgs
{
    public List<EntityUid> FiredProjectiles = default!;

    /// <summary>
    /// Set by fire mode system. True if this was a lobbed/indirect-fire shot.
    /// </summary>
    public bool Lobbed;
}

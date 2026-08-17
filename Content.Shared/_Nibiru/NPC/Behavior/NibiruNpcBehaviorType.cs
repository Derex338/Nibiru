namespace Content.Shared._Nibiru.NPC.Behavior;

/// <summary>
/// Base type of behavior.
/// </summary>
public enum NibiruNpcBehaviorType : byte
{
    /// <summary>
    /// Attacks hostile creatures when detected.
    /// </summary>
    Aggressive,

    /// <summary>
    /// Attacks only in response to aggression or when specific conditions are met.
    /// </summary>
    Neutral,

    /// <summary>
    /// Never attacks. Runs away when taking damage.
    /// </summary>
    Passive,

    /// <summary>
    /// Runs away from players and threats when detected in the field of view.
    /// </summary>
    Shy
}

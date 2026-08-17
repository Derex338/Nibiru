using Robust.Shared.GameStates;

namespace Content.Shared._Nibiru.NPC.Behavior.Components;

/// <summary>
/// Specific parameters of each style are stored in separate components:
/// - Default: no additional components
/// - HitAndLeap: <see cref="NibiruNpcHitAndRunAttackComponent"/>
/// - Charge: <see cref="NibiruNpcChargeAttackComponent"/>
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NibiruNpcCombatComponent : Component
{
    /// <summary>
    /// Combat style of this animal.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public NibiruCombatStyle CombatStyle = NibiruCombatStyle.Default;

    // Default style parameters

    /// <summary>
    /// Distance the animal retreats after an attack (Default-style).
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public float PostAttackRetreatDistance = 1.5f;

    /// <summary>
    /// Duration of the pause after retreating before the next attack (Default-style).
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public float PostAttackCooldown = 0.8f;

    // Runtime-state (general)

    /// <summary>
    /// Attack cooldown timer (used in Default-style for retreat pause).
    /// </summary>
    [ViewVariables]
    public float RetreatTimer;

    /// <summary>
    /// True if currently performing a retreat phase (Default-style).
    /// </summary>
    [ViewVariables]
    public bool IsRetreating;
}

using Robust.Shared.GameStates;

namespace Content.Shared._Nibiru.WeaponAttackType;

/// <summary>
/// Added to weapons that support multiple attack types.
/// Defines available attack type prototype IDs and tracks the currently selected one.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class NibiruWeaponAttackComponent : Component
{
    /// <summary>
    /// List of available attack type prototype IDs.
    /// </summary>
    [DataField(required: true)]
    [AutoNetworkedField]
    public List<string> AvailableAttacks = new();

    /// <summary>
    /// Index into AvailableAttacks for the currently selected attack type.
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public int CurrentAttackIndex = 0;

    /// <summary>
    /// Whether the player can cycle through attack types with the hotkey.
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public bool Cycleable = true;

    /// <summary>
    /// The held prefix the item had before attack types started changing shield stance visuals.
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public string? DefaultHeldPrefix;

    /// <summary>
    /// Original sprite state before attack type changed it.
    /// Used to restore sprite when switching back to modes without SpriteState override.
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public string? OriginalSpriteState;
}

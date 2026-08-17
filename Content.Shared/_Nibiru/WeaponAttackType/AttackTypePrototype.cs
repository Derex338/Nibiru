using Content.Shared.Damage;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Nibiru.WeaponAttackType;

[Prototype]
public sealed partial class AttackTypePrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = default!;

    /// <summary>
    /// Localized name shown in UI.
    /// </summary>
    [DataField]
    public string Name = string.Empty;

    /// <summary>
    /// Icon for the attack type button in UI.
    /// </summary>
    [DataField]
    public SpriteSpecifier? Icon;

    /// <summary>
    /// Damage multiplier applied to base weapon damage.
    /// </summary>
    [DataField]
    public float DamageMultiplier = 1f;

    /// <summary>
    /// Range multiplier applied to weapon range.
    /// </summary>
    [DataField]
    public float RangeMultiplier = 1f;

    /// <summary>
    /// Override attack angle. Null uses weapon's default angle.
    /// </summary>
    [DataField]
    public Angle? AngleOverride;

    /// <summary>
    /// Animation entity prototype ID (e.g. "WeaponArcSlash", "WeaponArcThrust").
    /// </summary>
    [DataField]
    public string Animation = string.Empty;

    /// <summary>
    /// Override swing sound.
    /// </summary>
    [DataField]
    public SoundSpecifier? SwingSound;

    /// <summary>
    /// Override hit sound.
    /// </summary>
    [DataField]
    public SoundSpecifier? HitSound;

    /// <summary>
    /// Completely replaces weapon base damage with this set (types & values).
    /// If null, uses weapon's own damage (adjusted by BonusDamage and DamageMultiplier).
    /// </summary>
    [DataField]
    public DamageSpecifier? DamageOverride;

    /// <summary>
    /// True = AoE arc attack (heavy), False = single-target attack (light).
    /// </summary>
    [DataField]
    public bool IsHeavyAttack;

    /// <summary>
    /// Stamina damage multiplier applied on hit.
    /// </summary>
    [DataField]
    public float StaminaDamageMultiplier = 1f;

    /// <summary>
    /// Attack rate multiplier (applied to weapon's attack rate).
    /// </summary>
    [DataField]
    public float AttackRateMultiplier = 1f;

    /// <summary>
    /// If true, enables lobbed/indirect-fire mode on GunComponent.
    /// </summary>
    [DataField]
    public bool Lobbed;

    /// <summary>
    /// Optional shield stance selected by this attack type.
    /// </summary>
    [DataField]
    public ShieldAttackMode ShieldMode = ShieldAttackMode.None;

    /// <summary>
    /// Optional held-prefix override while this shield mode is selected.
    /// </summary>
    [DataField]
    public string? ShieldHeldPrefix;

    /// <summary>
    /// Optional sprite state override when this attack type is selected.
    /// If specified, weapon sprite changes to this state when mode is active.
    /// Original state is restored when switching to another mode or None.
    /// </summary>
    [DataField]
    public string? SpriteState;
}

public enum ShieldAttackMode : byte
{
    None,
    Normal,
    Guard,
    Overhead,
}

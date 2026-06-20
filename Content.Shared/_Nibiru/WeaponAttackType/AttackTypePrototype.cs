using Content.Shared.Damage;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Nibiru.WeaponAttackType;

[Prototype("attackType")]
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
    /// Example: тычок копья даёт Piercing, а не Slash.
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
}

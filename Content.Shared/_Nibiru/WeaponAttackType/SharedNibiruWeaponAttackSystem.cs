using System.Diagnostics.CodeAnalysis;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Nibiru.WeaponAttackType;

public abstract partial class SharedNibiruWeaponAttackSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NibiruWeaponAttackComponent, ComponentInit>(OnComponentInit);

        // Integrate with melee weapon system via events
        SubscribeLocalEvent<NibiruWeaponAttackComponent, GetMeleeDamageEvent>(OnGetDamage);
        SubscribeLocalEvent<NibiruWeaponAttackComponent, GetMeleeAttackRateEvent>(OnGetAttackRate);
        SubscribeLocalEvent<NibiruWeaponAttackComponent, MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<NibiruWeaponAttackComponent, AttemptMeleeEvent>(OnAttemptMelee);

        // Integrate with ranged weapon system
        SubscribeLocalEvent<NibiruWeaponAttackComponent, ShotAttemptedEvent>(OnShotAttempted);
    }

    private void OnComponentInit(EntityUid uid, NibiruWeaponAttackComponent component, ComponentInit args)
    {
        if (component.CurrentAttackIndex >= component.AvailableAttacks.Count)
            component.CurrentAttackIndex = 0;
    }

    /// <summary>
    /// Apply damage from current attack type.
    /// DamageOverride полностью заменяет базовый урон оружия (типы и значения).
    /// Иначе работает DamageMultiplier.
    /// </summary>
    private void OnGetDamage(EntityUid uid, NibiruWeaponAttackComponent component, ref GetMeleeDamageEvent args)
    {
        if (!TryGetCurrentAttackType(component, out var attackType))
            return;

        if (attackType.DamageOverride != null)
            args.Damage = new(attackType.DamageOverride);
        else if (attackType.DamageMultiplier != 1f)
            args.Damage *= attackType.DamageMultiplier;
    }

    /// <summary>
    /// Apply attack rate multiplier from current attack type.
    /// </summary>
    private void OnGetAttackRate(EntityUid uid, NibiruWeaponAttackComponent component, ref GetMeleeAttackRateEvent args)
    {
        if (!TryGetCurrentAttackType(component, out var attackType))
            return;

        if (attackType.AttackRateMultiplier != 1f)
            args.Rate *= attackType.AttackRateMultiplier;
    }

    /// <summary>
    /// On hit: override sounds and apply stamina multiplier.
    /// </summary>
    private void OnMeleeHit(EntityUid uid, NibiruWeaponAttackComponent component, MeleeHitEvent args)
    {
        if (!TryGetCurrentAttackType(component, out var attackType))
            return;

        // Override hit sound
        if (attackType.HitSound != null)
            args.HitSoundOverride = attackType.HitSound;
    }

    /// <summary>
    /// Override weapon animation and angle on each attack based on current attack type.
    /// This runs on both client and server right before the attack is executed.
    /// </summary>
    private void OnAttemptMelee(EntityUid uid, NibiruWeaponAttackComponent component, AttemptMeleeEvent args)
    {
        if (!TryComp<MeleeWeaponComponent>(uid, out var melee))
            return;

        if (!TryGetCurrentAttackType(component, out var proto))
            return;

        // Подменяем анимацию — будет использована в DoLungeAnimation
        if (!string.IsNullOrEmpty(proto.Animation))
        {
            melee.Animation = proto.Animation;
            melee.WideAnimation = proto.Animation;
        }

        // Подменяем угол размаха
        if (proto.AngleOverride.HasValue)
            melee.Angle = proto.AngleOverride.Value;

        Dirty(uid, melee);
    }

    /// <summary>
    /// Apply lobbed flag to ShotAttemptedEvent when firing with a lobbed attack type.
    /// Also blocks lobbed shots if there's a roof overhead.
    /// </summary>
    private void OnShotAttempted(EntityUid uid, NibiruWeaponAttackComponent component, ref ShotAttemptedEvent args)
    {
        if (!TryGetCurrentAttackType(component, out var proto))
            return;

        args.Lobbed = proto.Lobbed;

        // Block lobbed shot if under roof (server-side check)
        if (proto.Lobbed && IsUnderRoof(uid, args.User))
        {
            args.Cancel();
        }
    }

    /// <summary>
    /// Optional roof check for lobbed shots. Override on server for actual check.
    /// Shared returns false (no roof blocking).
    /// </summary>
    protected virtual bool IsUnderRoof(EntityUid weapon, EntityUid user)
    {
        return false;
    }

    /// <summary>
    /// Get the current AttackTypePrototype from the component.
    /// </summary>
    public bool TryGetCurrentAttackType(NibiruWeaponAttackComponent component, [NotNullWhen(true)] out AttackTypePrototype? proto)
    {
        proto = null;

        if (component.AvailableAttacks.Count == 0)
            return false;

        if (component.CurrentAttackIndex < 0 || component.CurrentAttackIndex >= component.AvailableAttacks.Count)
            component.CurrentAttackIndex = 0;

        var protoId = component.AvailableAttacks[component.CurrentAttackIndex];
        return _proto.TryIndex(protoId, out proto);
    }

    /// <summary>
    /// Get a specific attack type prototype by ID.
    /// </summary>
    public AttackTypePrototype? GetAttackType(string id)
    {
        return _proto.TryIndex(id, out AttackTypePrototype? proto) ? proto : null;
    }

    /// <summary>
    /// Cycle to the next attack type in the list.
    /// </summary>
    public void CycleAttackType(EntityUid uid, NibiruWeaponAttackComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        if (!component.Cycleable || component.AvailableAttacks.Count <= 1)
            return;

        component.CurrentAttackIndex = (component.CurrentAttackIndex + 1) % component.AvailableAttacks.Count;
        Dirty(uid, component);

        // Notify client of the change
    }
}

/// <summary>
/// Message sent to server when client wants to cycle attack type.
/// </summary>
[Serializable, NetSerializable]
public sealed class CycleAttackTypeMessage : EntityEventArgs
{
    public readonly NetEntity Weapon;

    public CycleAttackTypeMessage(NetEntity weapon)
    {
        Weapon = weapon;
    }
}

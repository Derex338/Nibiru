using System.Linq;
using System.Numerics;
using Content.Shared._Nibiru.WeaponAttackType;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands.Components;
using Content.Shared.Inventory;
using Content.Shared.Timing;
using Content.Shared.Weapons.Melee;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Random;

namespace Content.Shared.Blocking;

public sealed partial class BlockingSystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private UseDelaySystem _useDelay = default!;

    private const string PassiveShieldBlockDelay = "passive-shield-block";

    private void InitializeUser()
    {
        SubscribeLocalEvent<BlockingUserComponent, DamageModifyEvent>(OnUserDamageModified);
        SubscribeLocalEvent<BlockingComponent, DamageModifyEvent>(OnDamageModified);
        SubscribeLocalEvent<BlockingComponent, InventoryRelayedEvent<DamageModifyEvent>>(OnInventoryDamageModified);

        SubscribeLocalEvent<BlockingUserComponent, EntParentChangedMessage>(OnParentChanged);
        SubscribeLocalEvent<BlockingUserComponent, ContainerGettingInsertedAttemptEvent>(OnInsertAttempt);
        SubscribeLocalEvent<BlockingUserComponent, AnchorStateChangedEvent>(OnAnchorChanged);
        SubscribeLocalEvent<BlockingUserComponent, EntityTerminatingEvent>(OnEntityTerminating);
    }

    private void OnParentChanged(EntityUid uid, BlockingUserComponent component, ref EntParentChangedMessage args)
    {
        UserStopBlocking(uid, component);
    }

    private void OnInsertAttempt(EntityUid uid, BlockingUserComponent component, ContainerGettingInsertedAttemptEvent args)
    {
        UserStopBlocking(uid, component);
    }

    private void OnAnchorChanged(EntityUid uid, BlockingUserComponent component, ref AnchorStateChangedEvent args)
    {
        if (args.Anchored)
            return;

        UserStopBlocking(uid, component);
    }

    private void OnUserDamageModified(EntityUid uid, BlockingUserComponent component, DamageModifyEvent args)
    {
        if (component.BlockingItem is not { } item || !TryComp<BlockingComponent>(item, out var blocking))
            return;

        if (args.Damage.GetTotal() <= 0)
            return;

        if (blocking.UseAttackTypeModes && !blocking.IsBlocking)
        {
            if (TryOverheadBlock(uid, item, blocking, args))
                return;

            TryPassiveHandBlock(uid, item, blocking, args);
            return;
        }

        // A shield should only block damage it can itself absorb. To determine that we need the Damageable component on it.
        if (!TryComp<DamageableComponent>(item, out var dmgComp))
            return;

        var blockFraction = blocking.IsBlocking ? blocking.ActiveBlockFraction : blocking.PassiveBlockFraction;
        var modifier = blocking.IsBlocking ? blocking.ActiveBlockDamageModifier : blocking.PassiveBlockDamageModifer;
        blockFraction = Math.Clamp(blockFraction, 0, 1);
        _damageable.TryChangeDamage((item, dmgComp), blockFraction * args.OriginalDamage);

        var modify = new DamageModifierSet();
        foreach (var key in modifier.Coefficients.Keys.Concat(modifier.FlatReduction.Keys))
        {
            modify.Coefficients.TryAdd(key, 1 - blockFraction);
        }

        args.Damage = DamageSpecifier.ApplyModifierSet(args.Damage, modify);

        if (blocking.IsBlocking && !args.Damage.Equals(args.OriginalDamage))
        {
            _audio.PlayPvs(blocking.BlockSound, uid);
        }
    }

    private void TryPassiveHandBlock(EntityUid user, EntityUid item, BlockingComponent blocking, DamageModifyEvent args)
    {
        if (blocking.CurrentMode != ShieldAttackMode.Normal)
            return;

        if (!_handsSystem.TryGetActiveItem(user, out var activeItem) || activeItem != item)
            return;

        if (!IsMeleeDamageFromFront(user, args.Origin))
            return;

        EnsureComp<UseDelayComponent>(item, out var useDelay);
        _useDelay.SetLength((item, useDelay), blocking.PassiveHandBlockCooldown, PassiveShieldBlockDelay);
        if (_useDelay.IsDelayed((item, useDelay), PassiveShieldBlockDelay))
            return;

        if (!_random.Prob(Math.Clamp(blocking.PassiveHandBlockChance, 0f, 1f)))
            return;

        ApplyShieldDamageSplit(user, item, blocking.PassiveHandBlockModifier ?? blocking.PassiveBlockDamageModifer,
            blocking.PassiveHandBlockFraction, args);

        _useDelay.TryResetDelay((item, useDelay), id: PassiveShieldBlockDelay);
        _audio.PlayPvs(blocking.BlockSound, user);
    }

    private bool TryOverheadBlock(EntityUid user, EntityUid item, BlockingComponent blocking, DamageModifyEvent args)
    {
        if (blocking.CurrentMode != ShieldAttackMode.Overhead)
            return false;

        if (!_handsSystem.TryGetActiveItem(user, out var activeItem) || activeItem != item)
            return false;

        if (IsMeleeDamage(args.Origin))
            return false;

        ApplyShieldDamageSplit(user, item, blocking.ActiveBlockDamageModifier, blocking.ActiveBlockFraction, args);
        _audio.PlayPvs(blocking.BlockSound, user);
        return true;
    }

    private bool IsMeleeDamage(EntityUid? origin)
    {
        return origin != null
               && TryComp<HandsComponent>(origin, out var hands)
               && _handsSystem.TryGetActiveItem((origin.Value, hands), out var weapon)
               && HasComp<MeleeWeaponComponent>(weapon.Value);
    }

    private bool IsMeleeDamageFromFront(EntityUid user, EntityUid? origin)
    {
        if (origin == null || origin == user)
            return false;

        if (!IsMeleeDamage(origin))
            return false;

        var userCoords = _transformSystem.GetMapCoordinates(user);
        var originCoords = _transformSystem.GetMapCoordinates(origin.Value);
        if (userCoords.MapId != originCoords.MapId)
            return false;

        var toAttacker = originCoords.Position - userCoords.Position;
        if (toAttacker.LengthSquared() <= 0.001f)
            return true;

        var facing = _transformSystem.GetWorldRotation(user).ToWorldVec();
        return Vector2.Dot(Vector2.Normalize(toAttacker), facing) > -0.25f;
    }

    private void ApplyShieldDamageSplit(EntityUid user, EntityUid item, DamageModifierSet modifier, float blockFraction, DamageModifyEvent args)
    {
        if (!TryComp<DamageableComponent>(item, out var dmgComp))
            return;

        blockFraction = Math.Clamp(blockFraction, 0, 1);
        _damageable.TryChangeDamage((item, dmgComp), blockFraction * args.OriginalDamage);

        var modify = new DamageModifierSet();
        foreach (var key in modifier.Coefficients.Keys.Concat(modifier.FlatReduction.Keys))
        {
            modify.Coefficients.TryAdd(key, 1 - blockFraction);
        }

        args.Damage = DamageSpecifier.ApplyModifierSet(args.Damage, modify);
    }

    private void OnInventoryDamageModified(EntityUid uid, BlockingComponent component, InventoryRelayedEvent<DamageModifyEvent> args)
    {
        if (!component.UseAttackTypeModes || component.BackBlockModifier == null)
            return;

        if (component.CurrentMode != ShieldAttackMode.Normal)
            return;

        if (!_inventory.InSlotWithFlags(uid, SlotFlags.BACK))
            return;

        if (args.Args.Origin == null || !IsBehind(args.Owner, args.Args.Origin.Value))
            return;

        ApplyShieldDamageSplit(args.Owner, uid, component.BackBlockModifier, component.BackBlockFraction, args.Args);
    }

    private bool IsBehind(EntityUid user, EntityUid origin)
    {
        var userCoords = _transformSystem.GetMapCoordinates(user);
        var originCoords = _transformSystem.GetMapCoordinates(origin);
        if (userCoords.MapId != originCoords.MapId)
            return false;

        var toAttacker = originCoords.Position - userCoords.Position;
        if (toAttacker.LengthSquared() <= 0.001f)
            return false;

        var facing = _transformSystem.GetWorldRotation(user).ToWorldVec();
        return Vector2.Dot(Vector2.Normalize(toAttacker), facing) < -0.25f;
    }

    private void OnDamageModified(EntityUid uid, BlockingComponent component, DamageModifyEvent args)
    {
        var modifier = component.IsBlocking ? component.ActiveBlockDamageModifier : component.PassiveBlockDamageModifer;
        if (modifier == null)
        {
            return;
        }

        args.Damage = DamageSpecifier.ApplyModifierSet(args.Damage, modifier);
    }

    private void OnEntityTerminating(EntityUid uid, BlockingUserComponent component, ref EntityTerminatingEvent args)
    {
        if (!TryComp<BlockingComponent>(component.BlockingItem, out var blockingComponent))
            return;

        StopBlockingHelper(component.BlockingItem.Value, blockingComponent, uid);

    }

    /// <summary>
    /// Check for the shield and has the user stop blocking
    /// Used where you'd like the user to stop blocking, but also don't want to remove the <see cref="BlockingUserComponent"/>
    /// </summary>
    /// <param name="uid">The user blocking</param>
    /// <param name="component">The <see cref="BlockingUserComponent"/></param>
    private void UserStopBlocking(EntityUid uid, BlockingUserComponent component)
    {
        if (TryComp<BlockingComponent>(component.BlockingItem, out var blockComp) && blockComp.IsBlocking)
            StopBlocking(component.BlockingItem.Value, blockComp, uid);
    }
}

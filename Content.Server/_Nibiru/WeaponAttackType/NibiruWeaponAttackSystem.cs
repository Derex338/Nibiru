using Content.Shared._Nibiru.WeaponAttackType;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Hands.Components;
using Robust.Shared.Serialization;

namespace Content.Server._Nibiru.WeaponAttackType;

public sealed class NibiruWeaponAttackSystem : SharedNibiruWeaponAttackSystem
{
    [Dependency] private readonly SharedHandsSystem _hands = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Handle client-requested cycle
        SubscribeNetworkEvent<CycleAttackTypeMessage>(OnCycleRequest);
    }

    private void OnCycleRequest(CycleAttackTypeMessage msg, EntitySessionEventArgs args)
    {
        var weapon = GetEntity(msg.Weapon);

        if (!TryComp<NibiruWeaponAttackComponent>(weapon, out var component))
            return;

        // Validate ownership - only the holder can cycle
        var user = args.SenderSession.AttachedEntity;
        if (user == null)
            return;

        // Check weapon is in the user's hand
        if (!IsWeaponInHand(user.Value, weapon))
            return;

        CycleAttackType(weapon, component);
    }

    private bool IsWeaponInHand(EntityUid user, EntityUid weapon)
    {
        if (!TryComp<HandsComponent>(user, out var hands))
            return false;

        foreach (var handId in hands.SortedHands)
        {
            var held = _hands.GetHeldItem(user, handId);
            if (held == weapon)
                return true;
        }

        return false;
    }
}

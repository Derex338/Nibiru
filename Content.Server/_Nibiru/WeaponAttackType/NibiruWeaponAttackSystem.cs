using Content.Shared._Nibiru.WeaponAttackType;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Serialization;

namespace Content.Server._Nibiru.WeaponAttackType;

public sealed class NibiruWeaponAttackSystem : SharedNibiruWeaponAttackSystem
{
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedRoofSystem _roof = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IMapManager _map = default!;

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

    /// <summary>
    /// Server-side roof check. Blocks lobbed shots if shooter or target tile is under roof.
    /// </summary>
    protected override bool IsUnderRoof(EntityUid weapon, EntityUid user)
    {
        var userPos = _transform.GetMapCoordinates(user);
        if (!_map.TryFindGridAt(userPos, out var gridUid, out var grid))
            return false;

        if (!TryComp<RoofComponent>(gridUid, out var roof))
            return false;

        // Convert world position to tile indices
        var tileIndices = _transform.GetGridOrMapTilePosition(user);
        return _roof.IsRooved((gridUid, grid, roof), tileIndices);
    }
}

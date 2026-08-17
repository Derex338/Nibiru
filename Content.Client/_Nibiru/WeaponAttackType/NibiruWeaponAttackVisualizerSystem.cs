using System.Linq;
using Content.Shared._Nibiru.WeaponAttackType;
using Robust.Client.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Client._Nibiru.WeaponAttackType;

/// <summary>
/// Client-only sysytem for changes the sprites of weapons depending on the selected attack type.
/// Subscribes to changes in NibiruWeaponAttackComponent and applies SpriteState from AttackTypePrototype.
/// </summary>
public sealed partial class NibiruWeaponAttackVisualizerSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    private EntityQuery<SpriteComponent> _spriteQuery;

    public override void Initialize()
    {
        base.Initialize();

        _spriteQuery = GetEntityQuery<SpriteComponent>();

        SubscribeLocalEvent<NibiruWeaponAttackComponent, ComponentStartup>(OnComponentStartup);
        SubscribeLocalEvent<NibiruWeaponAttackComponent, AfterAutoHandleStateEvent>(OnHandleState);
    }

    private void OnComponentStartup(EntityUid uid, NibiruWeaponAttackComponent component, ComponentStartup args)
    {
        // Applying initial sprite when the component is initialized
        ApplySpriteState(uid, component);
    }

    private void OnHandleState(EntityUid uid, NibiruWeaponAttackComponent component, ref AfterAutoHandleStateEvent args)
    {
        // Applying sprite when getting an update from the server
        ApplySpriteState(uid, component);
    }

    private void ApplySpriteState(EntityUid uid, NibiruWeaponAttackComponent component)
    {
        if (!_spriteQuery.TryComp(uid, out var spriteComp))
            return;

        if (!TryGetCurrentAttackType(component, out var proto))
            return;

        if (proto is not null &&!string.IsNullOrEmpty(proto.SpriteState))
        {
            // Saving the original state if it hasn't been saved yet
            if (component.OriginalSpriteState == null && spriteComp.AllLayers.Count() > 0)
            {
                var state = _sprite.LayerGetRsiState((uid, spriteComp), 0);
                if (state.IsValid)
                    component.OriginalSpriteState = state.Name;
            }

            // Applying new state to all layers
            for (var i = 0; i < spriteComp.AllLayers.Count(); i++)
            {
                _sprite.LayerSetRsiState((uid, spriteComp), i, proto.SpriteState);
            }
        }
        else if (component.OriginalSpriteState != null)
        {
            // Restoring the original state
            for (var i = 0; i < spriteComp.AllLayers.Count(); i++)
            {
                _sprite.LayerSetRsiState((uid, spriteComp), i, component.OriginalSpriteState);
            }

            component.OriginalSpriteState = null;
        }
    }

    private bool TryGetCurrentAttackType(NibiruWeaponAttackComponent component, out AttackTypePrototype? proto)
    {
        proto = null;

        if (component.AvailableAttacks.Count == 0)
            return false;

        if (component.CurrentAttackIndex < 0 || component.CurrentAttackIndex >= component.AvailableAttacks.Count)
            return false;

        var protoId = component.AvailableAttacks[component.CurrentAttackIndex];
        return _proto.TryIndex(protoId, out proto);
    }
}

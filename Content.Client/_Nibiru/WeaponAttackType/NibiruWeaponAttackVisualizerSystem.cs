using System.Linq;
using Content.Shared._Nibiru.WeaponAttackType;
using Robust.Client.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Client._Nibiru.WeaponAttackType;

/// <summary>
/// Client-only система для изменения спрайтов оружия в зависимости от выбранного типа атаки.
/// Подписывается на изменения NibiruWeaponAttackComponent и применяет SpriteState из AttackTypePrototype.
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
        // Применяем начальный спрайт при инициализации компонента
        ApplySpriteState(uid, component);
    }

    private void OnHandleState(EntityUid uid, NibiruWeaponAttackComponent component, ref AfterAutoHandleStateEvent args)
    {
        // Применяем спрайт при получении обновления состояния с сервера
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
            // Сохраняем оригинальное состояние если ещё не сохранено
            if (component.OriginalSpriteState == null && spriteComp.AllLayers.Count() > 0)
            {
                var state = _sprite.LayerGetRsiState((uid, spriteComp), 0);
                if (state.IsValid)
                    component.OriginalSpriteState = state.Name;
            }

            // Применяем новое состояние ко всем слоям
            for (var i = 0; i < spriteComp.AllLayers.Count(); i++)
            {
                _sprite.LayerSetRsiState((uid, spriteComp), i, proto.SpriteState);
            }
        }
        else if (component.OriginalSpriteState != null)
        {
            // Восстанавливаем оригинальное состояние
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

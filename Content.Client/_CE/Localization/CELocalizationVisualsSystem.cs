using Robust.Client.GameObjects;
using Robust.Shared.Localization;

namespace Content.Client._CE.Localization;

public sealed class CELocalizationVisualsSystem : EntitySystem
{
    [Dependency] private readonly ILocalizationManager _loc = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CELocalizationVisualsComponent, ComponentStartup>(OnStartup);
    }

    private void OnStartup(Entity<CELocalizationVisualsComponent> entity, ref ComponentStartup args)
    {
        if (!TryComp(entity, out SpriteComponent? sprite))
            return;

        var culture = _loc.DefaultCulture?.Name;
        if (string.IsNullOrEmpty(culture))
            return;

        foreach (var (layerKey, cultureStates) in entity.Comp.MapStates)
        {
            if (!cultureStates.TryGetValue(culture, out var state))
                continue;

            if (!_sprite.LayerMapTryGet((entity.Owner, sprite), layerKey, out var layer, false))
                continue;

            _sprite.LayerSetRsiState((entity.Owner, sprite), layer, state);
        }
    }
}

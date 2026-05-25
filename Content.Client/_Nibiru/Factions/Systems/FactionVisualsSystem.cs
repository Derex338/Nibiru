using Content.Client._Nibiru.Systems;
using Content.Shared._Nibiru.Factions;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;

namespace Content.Client._Nibiru.Factions.Systems;

public sealed class FactionVisualsSystem : EntitySystem
{
    [Dependency] private readonly TextureGenerationSystem _texGen = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FactionVisualsComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<FactionVisualsComponent, AfterAutoHandleStateEvent>(OnHandleState);
    }

    private void OnStartup(EntityUid uid, FactionVisualsComponent component, ComponentStartup args)
    {
        UpdateVisuals(uid, component);
    }

    private void OnHandleState(Entity<FactionVisualsComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        UpdateVisuals(ent, ent.Comp);
    }

    public void UpdateVisuals(EntityUid uid, FactionVisualsComponent component, SpriteComponent? sprite = null)
    {
        if (!Resolve(uid, ref sprite, false))
            return;

        if (component.LogoPixels == null || component.LogoPixels.Count != 16 * 16)
        {
            // Логотипа нет — показываем фон в оригинальном/перекрашенном виде
            if (sprite.LayerMapTryGet(FactionVisualLayers.Background, out var bgLayerOnly))
            {
                sprite.LayerSetVisible(bgLayerOnly, true);
                sprite.LayerSetColor(bgLayerOnly, component.LogoBackground);
            }

            if (sprite.LayerMapTryGet(FactionVisualLayers.Logo, out var logoLayerHide))
                sprite.LayerSetVisible(logoLayerHide, false);
            return;
        }

        // Перекрашиваем фон
        if (sprite.LayerMapTryGet(FactionVisualLayers.Background, out var bgLayer))
        {
            sprite.LayerSetVisible(bgLayer, true);
            sprite.LayerSetColor(bgLayer, component.LogoBackground);
        }

        // Генерируем и применяем текстуру логотипа
        if (sprite.LayerMapTryGet(FactionVisualLayers.Logo, out var logoLayer))
        {
            sprite.LayerSetVisible(logoLayer, true);

            var texture = _texGen.GenerateTexture(component.LogoPixels, "FactionLogo");

            if (texture != null && sprite[logoLayer].Texture != texture)
                sprite.LayerSetTexture(logoLayer, texture);
        }
    }
}

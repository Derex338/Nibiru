using Content.Client._Nibiru.Systems;
using Content.Shared._Nibiru.Factions;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;

namespace Content.Client._Nibiru.Factions.Systems;

public sealed partial class FactionVisualsSystem : EntitySystem
{
    [Dependency] private TextureGenerationSystem _texGen = default!;
    [Dependency] private IOverlayManager _overlayManager = default!;
    [Dependency] private IPlayerManager _playerManager = default!;

    private FactionIconOverlay? _overlay;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FactionVisualsComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<FactionVisualsComponent, AfterAutoHandleStateEvent>(OnHandleState);

        var logoSystem = EntityManager.System<NibiruFactionLogoSystem>();
        var transformSystem = EntityManager.System<TransformSystem>();
        var spriteSystem = EntityManager.System<SpriteSystem>();

        _overlay = new FactionIconOverlay(EntityManager, _playerManager, logoSystem, transformSystem, spriteSystem);
        _overlayManager.AddOverlay(_overlay);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        if (_overlay != null)
        {
            _overlayManager.RemoveOverlay(_overlay);
            _overlay = null;
        }
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
            // No logo - show the background in its original/recolored form
            if (sprite.LayerMapTryGet(FactionVisualLayers.Background, out var bgLayerOnly))
            {
                sprite.LayerSetVisible(bgLayerOnly, true);
                sprite.LayerSetColor(bgLayerOnly, component.LogoBackground);
            }

            if (sprite.LayerMapTryGet(FactionVisualLayers.Logo, out var logoLayerHide))
                sprite.LayerSetVisible(logoLayerHide, false);
            return;
        }

        // Recolor background
        if (sprite.LayerMapTryGet(FactionVisualLayers.Background, out var bgLayer))
        {
            sprite.LayerSetVisible(bgLayer, true);
            sprite.LayerSetColor(bgLayer, component.LogoBackground);
        }

        // Generate and apply the logo texture
        if (sprite.LayerMapTryGet(FactionVisualLayers.Logo, out var logoLayer))
        {
            sprite.LayerSetVisible(logoLayer, true);

            var texture = _texGen.GenerateTexture(component.LogoPixels, "FactionLogo");

            if (texture != null && sprite[logoLayer].Texture != texture)
                sprite.LayerSetTexture(logoLayer, texture);
        }
    }
}

using Content.Shared._Nibiru.NPC;
using Content.Shared._Nibiru.NPC.Livestock;
using Robust.Client.GameObjects;

namespace Content.Client._Nibiru.NPC.Systems;

/// <summary>
/// Updates the visual appearance of the animal based on its gender.
/// </summary>
public sealed partial class NibiruLivestockAppearanceSystem : EntitySystem
{
    [Dependency] private AppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<NibiruLivestockComponent, AppearanceChangeEvent>(OnAppearanceChange);
    }

    private void OnAppearanceChange(EntityUid uid, NibiruLivestockComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (_appearance.TryGetData<LivestockSex>(uid, LivestockVisuals.Sex, out var sex, args.Component))
        {
            var sprite = sex == LivestockSex.Male ? component.MaleSprite : component.FemaleSprite;
            if (sprite != null)
            {
                args.Sprite.LayerSetSprite(0, sprite);
            }
        }

        if (_appearance.TryGetData<bool>(uid, LivestockVisuals.IsLeashed, out var isLeashed, args.Component))
        {
            if (args.Sprite.LayerMapTryGet(LivestockVisualLayers.Leash, out var layer))
            {
                args.Sprite.LayerSetVisible(layer, isLeashed);
            }
            else if (isLeashed)
            {
                var newLayer = args.Sprite.AddLayer(new Robust.Shared.Utility.SpriteSpecifier.Rsi(new("/Textures/_Nibiru/Entities/Mobs/Rope.rsi"), "tied"));
                args.Sprite.LayerMapSet(LivestockVisualLayers.Leash, newLayer);
                args.Sprite.LayerSetVisible(newLayer, true);
            }
        }
    }

    public enum LivestockVisualLayers : byte
    {
        Leash
    }
}

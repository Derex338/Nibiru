using Content.Shared._Nibiru.Factions;
using Robust.Client.Graphics;
using Robust.Shared.Maths;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Robust.Shared.GameStates;
using Robust.Shared.GameObjects;
using Content.Shared._Nibiru.Factions.Messeges;

namespace Content.Client._Nibiru.Factions;

public sealed partial class NibiruFactionLogoSystem : EntitySystem
{
[Dependency] private IClyde _clyde = default!;

    // Кэш текстур логотипов фракций по названию фракции
    // Кэш текстур логотипов фракций по названию фракции
    private readonly Dictionary<string, OwnedTexture> _logoCache = new();
    private readonly Dictionary<string, OwnedTexture> _logo8x8Cache = new();

    public override void Initialize()
    {
        base.Initialize();

        //SubscribeLocalEvent<FactionComponent, AfterAutoHandleStateEvent>(OnHandleState);
    }

    private void OnHandleState(EntityUid uid, FactionComponent component, ref AfterAutoHandleStateEvent args)
    {
        // Обновляем текстуру при изменении стейта
        UpdateFactionLogo(component.FactionName, component.LogoBackground, component.LogoPixels, component.LogoPixels8x8);
    }

    public void UpdateFactionLogo(string factionName, Robust.Shared.Maths.Color bgColor, List<Robust.Shared.Maths.Color> pixels, List<Robust.Shared.Maths.Color>? pixels8x8 = null)
    {
        if (string.IsNullOrEmpty(factionName) || pixels == null || pixels.Count != 16 * 16)
            return;

        // Если все пиксели прозрачные и фон прозрачный, то логотипа нет
        bool hasPixels = false;
        for (int i = 0; i < pixels.Count; i++)
        {
            if (pixels[i] != Robust.Shared.Maths.Color.Transparent)
            {
                hasPixels = true;
                break;
            }
        }

        if (!hasPixels && bgColor == Robust.Shared.Maths.Color.Transparent)
        {
            if (_logoCache.TryGetValue(factionName, out var oldTex))
            {
                oldTex.Dispose();
                _logoCache.Remove(factionName);
            }
            if (_logo8x8Cache.TryGetValue(factionName, out var oldTex8))
            {
                oldTex8.Dispose();
                _logo8x8Cache.Remove(factionName);
            }
            return;
        }

        var image = new Image<Rgba32>(16, 16);
        for (int y = 0; y < 16; y++)
        {
            for (int x = 0; x < 16; x++)
            {
                var col = pixels[y * 16 + x];
                if (col == Robust.Shared.Maths.Color.Transparent)
                    col = bgColor;

                image[x, y] = new Rgba32(col.RByte, col.GByte, col.BByte, col.AByte);
            }
        }

        if (_logoCache.TryGetValue(factionName, out var tex))
        {
            tex.Dispose();
        }

        _logoCache[factionName] = _clyde.LoadTextureFromImage(image, "FactionLogo_" + factionName);

        if (pixels8x8 != null && pixels8x8.Count == 8 * 8)
        {
            var image8 = new Image<Rgba32>(8, 8);
            for (int y = 0; y < 8; y++)
            {
                for (int x = 0; x < 8; x++)
                {
                    var col = pixels8x8[y * 8 + x];
                    if (col == Robust.Shared.Maths.Color.Transparent)
                        col = bgColor;

                    image8[x, y] = new Rgba32(col.RByte, col.GByte, col.BByte, col.AByte);
                }
            }

            if (_logo8x8Cache.TryGetValue(factionName, out var tex8))
            {
                tex8.Dispose();
            }

            _logo8x8Cache[factionName] = _clyde.LoadTextureFromImage(image8, "FactionLogo8x8_" + factionName);
        }

        RaiseLocalEvent(new FactionLogoUpdatedEvent(factionName));
    }

    /// <summary>
    /// Возвращает сгенерированную текстуру логотипа фракции, которую можно использовать в SpriteComponent
    /// </summary>
    public Texture? GetFactionLogoTexture(string factionName)
    {
        if (_logoCache.TryGetValue(factionName, out var tex))
            return tex;

        return null;
    }

    /// <summary>
    /// Возвращает сгенерированную 8x8 текстуру логотипа фракции
    /// </summary>
    public Texture? GetFactionLogo8x8Texture(string factionName)
    {
        if (_logo8x8Cache.TryGetValue(factionName, out var tex))
            return tex;

        return null;
    }
}

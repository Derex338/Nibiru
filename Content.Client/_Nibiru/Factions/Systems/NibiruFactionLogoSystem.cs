using Content.Shared._Nibiru.Factions;
using Robust.Client.Graphics;
using Robust.Shared.Maths;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Robust.Shared.GameStates;
using Robust.Shared.GameObjects;

namespace Content.Client._Nibiru.Factions;

public sealed class NibiruFactionLogoSystem : EntitySystem
{
    [Dependency] private readonly IClyde _clyde = default!;

    // Кэш текстур логотипов фракций по названию фракции
    private readonly Dictionary<string, OwnedTexture> _logoCache = new();

    public override void Initialize()
    {
        base.Initialize();

        //SubscribeLocalEvent<FactionComponent, AfterAutoHandleStateEvent>(OnHandleState);
    }

    private void OnHandleState(EntityUid uid, FactionComponent component, ref AfterAutoHandleStateEvent args)
    {
        // Обновляем текстуру при изменении стейта
        UpdateFactionLogo(component.FactionName, component.LogoBackground, component.LogoPixels);
    }

    public void UpdateFactionLogo(string factionName, Robust.Shared.Maths.Color bgColor, List<Robust.Shared.Maths.Color> pixels)
    {
        if (string.IsNullOrEmpty(factionName) || pixels == null || pixels.Count != 32 * 32)
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
            return;
        }

        var image = new Image<Rgba32>(32, 32);
        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 32; x++)
            {
                var col = pixels[y * 32 + x];
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
}

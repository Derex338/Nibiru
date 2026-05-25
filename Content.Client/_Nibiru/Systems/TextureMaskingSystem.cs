using Robust.Client.Graphics;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Maths;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Color = Robust.Shared.Maths.Color;

namespace Content.Client._Nibiru.Systems;

/// <summary>
/// Утилитарная система для генерации текстур из пиксельных данных.
/// Результаты кешируются для повторного использования.
/// </summary>
public sealed class TextureGenerationSystem : EntitySystem
{
    [Dependency] private readonly IClyde _clyde = default!;

    private readonly Dictionary<int, Texture> _textureCache = new();

    /// <summary>
    /// Генерирует текстуру 16x16 из списка цветов.
    /// Результат кешируется по хешу входных данных.
    /// </summary>
    /// <param name="pixels">Список из 256 цветов (16x16)</param>
    /// <param name="name">Имя текстуры для отладки</param>
    /// <returns>Готовая текстура или null при невалидных данных</returns>
    public Texture? GenerateTexture(List<Color> pixels, string? name = null)
    {
        if (pixels.Count != 16 * 16)
            return null;

        int hash = ComputeHash(pixels);

        if (_textureCache.TryGetValue(hash, out var cached))
            return cached;

        var image = new Image<Rgba32>(16, 16);

        for (int y = 0; y < 16; y++)
        {
            for (int x = 0; x < 16; x++)
            {
                var col = pixels[y * 16 + x];
                image[x, y] = new Rgba32(col.RByte, col.GByte, col.BByte, col.AByte);
            }
        }

        var texture = _clyde.LoadTextureFromImage(image, name ?? $"GeneratedTex_{hash}");
        _textureCache[hash] = texture;
        return texture;
    }

    /// <summary>
    /// Очищает кеш текстур. Полезно при смене данных.
    /// </summary>
    public void ClearCache()
    {
        _textureCache.Clear();
    }

    private static int ComputeHash(List<Color> pixels)
    {
        unchecked
        {
            int hash = 17;
            foreach (var p in pixels)
                hash = hash * 31 + p.GetHashCode();

            return hash;
        }
    }
}

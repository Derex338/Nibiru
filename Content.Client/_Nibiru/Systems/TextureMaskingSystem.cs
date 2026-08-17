using Robust.Client.Graphics;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Maths;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Color = Robust.Shared.Maths.Color;

namespace Content.Client._Nibiru.Systems;

/// <summary>
/// Utility system for generating textures from pixel data.
/// Results are cached for reuse.
/// </summary>
public sealed partial class TextureGenerationSystem : EntitySystem
{
    [Dependency] private IClyde _clyde = default!;

    private readonly Dictionary<int, Texture> _textureCache = new();

    /// <summary>
    /// Generates a 16x16 texture from a list of colors.
    /// Results are cached by hash.
    /// </summary>
    /// <param name="pixels">List of 256 colors (16x16)</param>
    /// <param name="name">Texture name for debugging</param>
    /// <returns>Texture or null if invalid</returns>
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
    /// Clears texture cache. Useful when changing data.
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

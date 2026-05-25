using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Maths;

namespace Content.Client.UserInterface.Systems.Faction.UI;

public sealed class FactionLogoControl : Control
{
    private Color _bg = Color.Transparent;
    private List<Color>? _pixels;

    public void UpdateLogo(Color bg, List<Color>? pixels)
    {
        _bg = bg;
        _pixels = pixels;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var rect = PixelSizeBox;
        handle.DrawRect(rect, _bg);

        if (_pixels == null || _pixels.Count != 16 * 16)
            return;

        var pixelW = rect.Width / 16f;
        var pixelH = rect.Height / 16f;

        for (int y = 0; y < 16; y++)
        {
            for (int x = 0; x < 16; x++)
            {
                var c = _pixels[y * 16 + x];
                if (c == Color.Transparent)
                    continue;

                var r = new UIBox2(
                    rect.Left + x * pixelW,
                    rect.Top + y * pixelH,
                    rect.Left + (x + 1) * pixelW,
                    rect.Top + (y + 1) * pixelH);

                handle.DrawRect(r, c);
            }
        }
    }
}

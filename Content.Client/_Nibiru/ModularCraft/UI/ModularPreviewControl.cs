using Content.Shared._Nibiru.ModularCraft;
using Content.Shared._Nibiru.ModularCraft.Prototypes;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.Utility;
using Robust.Shared.ContentPack;
using Robust.Shared.Input;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Resources;
using Robust.Shared.Utility;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Numerics;
using Color = Robust.Shared.Maths.Color;

namespace Content.Client._Nibiru.ModularCraft.UI;

public sealed class SlotVisualState
{
    public bool    IsSelected;
    public bool    IsHovered;
    public string? ModuleId;
    public Color?  MaterialColor;
    public SpriteSpecifier? MaterialTexture;
}

/// <summary>
/// Preview control of the assembled item.
/// All module sprites are drawn on top of each other in the element's (0,0) coordinates.
/// The module sprite is repainted by pixels: the material color is applied
/// as Multiply to the opaque pixels of the material mask-texture.
/// Hit test on opaque sprite pixels (slot with maximum Z-order wins).
/// </summary>
public sealed class ModularPreviewControl : Control
{
    public event Action<string>? OnSlotHovered;
    public event Action<string>? OnSlotUnhovered;
    public event Action<string>? OnSlotClicked;

    private static readonly Color ColorHoverOutline = new(230, 180, 40, 220);
    private static readonly Color ColorDimOutline   = new(60, 60, 80, 120);

    private string? _itemType;

    // Order of slots - determines the Z-order (the last is drawn on top)
    private List<string> _slotOrder = new();

    private readonly Dictionary<string, SlotVisualState> _states = new();
    private string? _hoveredSlot;

    // Cache of textures after repainting with material
    private readonly Dictionary<string, Texture> _tintedCache  = new();
    private readonly Dictionary<string, string>  _cacheKeys     = new(); // slotId → "moduleId:materialColor"

    private IPrototypeManager   _proto;
    private SpriteSystem        _sprite;
    private IResourceCache       _res;
    private IResourceManager _resManager;
    private IClyde              _clyde;

    // Control size - all sprites are drawn in this area
    private const float W = 120f;
    private const float H = 340f;

    public ModularPreviewControl()
    {
        MinSize    = new Vector2(W, H);
        MouseFilter = MouseFilterMode.Stop;

        _proto       = IoCManager.Resolve<IPrototypeManager>();
        _sprite      = IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<SpriteSystem>();
        _res         = IoCManager.Resolve<IResourceCache>();
        _resManager  = IoCManager.Resolve<IResourceManager>();
        _clyde       = IoCManager.Resolve<IClyde>();
    }

    // API

    public void SetItemType(string type)
    {
        if (_itemType == type) return;
        _itemType = type;
        _states.Clear();
        _hoveredSlot = null;
        _tintedCache.Clear();
        _cacheKeys.Clear();
        RebuildOrder();
    }

    public void SetSlotState(string slot, SlotVisualState state)
    {
        _states[slot] = state;
        InvalidateTintCache(slot);
    }

    public void ClearSlots()
    {
        _states.Clear();
        _tintedCache.Clear();
        _cacheKeys.Clear();
    }

    public SlotVisualState? GetState(string slot) => _states.GetValueOrDefault(slot);

    // Slot order

    private void RebuildOrder()
    {
        _slotOrder.Clear();
        if (_itemType == null || !_proto.TryIndex<ModularItemPrototype>(_itemType, out var item))
            return;
        foreach (var part in item.RequiredParts)
            _slotOrder.Add(part.Id);
    }

    // Tint cache

    private void InvalidateTintCache(string slot)
    {
        if (_tintedCache.ContainsKey(slot))
        {
            _tintedCache.Remove(slot);
            _cacheKeys.Remove(slot);
        }
    }

    /// <summary>
    /// Returns the module texture repainted with material:
    /// — Take the source module sprite (RGBA)
    /// — For each pixel, apply Multiply with the material color
    /// — If there is a material mask texture, use its alpha as the effect intensity
    /// </summary>
    private Texture? GetTintedTexture(string slot, SlotVisualState state)
    {
        if (state.ModuleId == null)
            return null;

        if (!_proto.TryIndex<ModularModulePrototype>(state.ModuleId, out var module) || module.Sprite == null)
            return null;

        string matKey = state.MaterialColor.HasValue ? state.MaterialColor.Value.ToHex() : "none";
        string key    = $"{state.ModuleId}:{matKey}";

        if (_cacheKeys.TryGetValue(slot, out var cached) && cached == key && _tintedCache.ContainsKey(slot))
            return _tintedCache[slot];

        // Get the path to the RSI sprite
        if (module.Sprite is not SpriteSpecifier.Rsi rsiSpec)
        {
            // For Texture sprites, just draw directly with tint
            _cacheKeys[slot]  = key;
            _tintedCache[slot] = _sprite.Frame0(module.Sprite);
            return _tintedCache[slot];
        }

        // Load the RSI sprite PNG file directly for pixel access
        var pngPath = new ResPath("/Textures") / rsiSpec.RsiPath / $"{rsiSpec.RsiState}.png";
        if (!_resManager.TryContentFileRead(pngPath, out var stream))
            return null;

        Image<Rgba32> srcImage;
        using (stream)
        {
            srcImage = Image.Load<Rgba32>(stream);
        }

        var w = srcImage.Width;
        var h = srcImage.Height;
        var dst = new Image<Rgba32>(w, h);

        var srcSpan = srcImage.GetPixelSpan();
        var dstSpan = dst.GetPixelSpan();

        // Load the material mask PNG (if any)
        Span<Rgba32> maskSpan = default;
        int maskW = 0, maskH = 0;
        Image<Rgba32>? maskImage = null;
        if (state.MaterialTexture is SpriteSpecifier.Rsi maskRsi)
        {
            var maskPngPath = new ResPath("/Textures") / maskRsi.RsiPath / $"{maskRsi.RsiState}.png";
            if (_resManager.TryContentFileRead(maskPngPath, out var maskStream))
            {
                using (maskStream)
                {
                    maskImage = Image.Load<Rgba32>(maskStream);
                    maskW = maskImage.Width;
                    maskH = maskImage.Height;
                    maskSpan = maskImage.GetPixelSpan();
                }
            }
        }

        // Material color
        Color matColor = state.MaterialColor ?? Color.White;
        float mr = matColor.R;
        float mg = matColor.G;
        float mb = matColor.B;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int idx = y * w + x;
                var src = srcSpan[idx];
                if (src.A == 0)
                {
                    dstSpan[idx] = new Rgba32(0, 0, 0, 0);
                    continue;
                }

                // Material overlay factor from mask (0..1)
                float factor = 1f;
                if (maskImage != null && maskSpan.Length > 0)
                {
                    var mx = Math.Clamp(x, 0, maskW - 1);
                    var my = Math.Clamp(y, 0, maskH - 1);
                    int maskIdx = my * maskW + mx;
                    factor = maskSpan[maskIdx].A / 255f;
                }

                // Multiply blend between the original and the material color
                float nr = src.R / 255f;
                float ng = src.G / 255f;
                float nb = src.B / 255f;

                float finalR = MathHelper.Lerp(nr, nr * mr, factor);
                float finalG = MathHelper.Lerp(ng, ng * mg, factor);
                float finalB = MathHelper.Lerp(nb, nb * mb, factor);

                dstSpan[idx] = new Rgba32(
                    (byte)Math.Clamp(finalR * 255f, 0, 255),
                    (byte)Math.Clamp(finalG * 255f, 0, 255),
                    (byte)Math.Clamp(finalB * 255f, 0, 255),
                    src.A
                );
            }
        }

        maskImage?.Dispose();
        srcImage.Dispose();

        var tex = _clyde.LoadTextureFromImage(dst, $"modular_{slot}_{key}");
        _tintedCache[slot] = tex;
        _cacheKeys[slot]   = key;
        return tex;
    }

    // Render

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var drawArea = new UIBox2(0, 0, W, H);

        foreach (var slot in _slotOrder)
        {
            var state = _states.GetValueOrDefault(slot);

            if (state?.ModuleId != null)
            {
                var tex = GetTintedTexture(slot, state);
                if (tex != null)
                {
                    handle.DrawTextureRect(tex, drawArea);
                }
                else
                {
                    DrawPlaceholder(handle, slot, state);
                }
            }
            else
            {
                DrawPlaceholder(handle, slot, state);
            }
        }

        // Highlight the hovered slot over all sprites
        if (_hoveredSlot != null)
        {
            handle.DrawRect(drawArea, ColorHoverOutline.WithAlpha(0.25f));
        }
    }

    private void DrawPlaceholder(DrawingHandleScreen handle, string slot, SlotVisualState? state)
    {
        // A small gray rectangle in the middle to show that there is a slot
        var ph = new UIBox2(W * 0.3f, H * 0.05f, W * 0.7f, H * 0.95f);
        handle.DrawRect(ph, new Color(80, 80, 80, 40));
    }

    // Hit test: based on opaque pixels (the last slot in Z-order wins)

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);
        var hit = HitTest(args.RelativePosition);
        if (hit == _hoveredSlot) return;

        if (_hoveredSlot != null) OnSlotUnhovered?.Invoke(_hoveredSlot);
        _hoveredSlot = hit;
        if (hit != null) OnSlotHovered?.Invoke(hit);
    }

    protected override void MouseExited()
    {
        base.MouseExited();
        if (_hoveredSlot != null) OnSlotUnhovered?.Invoke(_hoveredSlot);
        _hoveredSlot = null;
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);
        if (args.Function != EngineKeyFunctions.UIClick) return;
        var hit = HitTest(args.RelativePosition);
        if (hit != null) OnSlotClicked?.Invoke(hit);
    }

    /// <summary>
    /// Iterate through slots in reverse order (topmost Z-first).
    /// For each, check if the cursor falls into an opaque pixel of the texture.
    /// </summary>
    private string? HitTest(Vector2 pos)
    {
        if (pos.X < 0 || pos.Y < 0 || pos.X >= W || pos.Y >= H)
            return null;

        for (int i = _slotOrder.Count - 1; i >= 0; i--)
        {
            var slot  = _slotOrder[i];
            var state = _states.GetValueOrDefault(slot);

            if (state?.ModuleId == null)
                continue;

            if (!_proto.TryIndex<ModularModulePrototype>(state.ModuleId, out var module) ||
                module.Sprite is not SpriteSpecifier.Rsi rsiSpec)
                continue;

            // Load PNG file directly for pixel check
            var pngPath = new ResPath("/Textures") / rsiSpec.RsiPath / $"{rsiSpec.RsiState}.png";
            if (!_resManager.TryContentFileRead(pngPath, out var stream))
                continue;

            Image<Rgba32> img;
            using (stream)
            {
                img = Image.Load<Rgba32>(stream);
            }

            var pixelSpan = img.GetPixelSpan();

            // Map UI coordinates to sprite pixels
            int px = (int)(pos.X / W * img.Width);
            int py = (int)(pos.Y / H * img.Height);
            px = Math.Clamp(px, 0, img.Width  - 1);
            py = Math.Clamp(py, 0, img.Height - 1);

            int idx = py * img.Width + px;
            if (pixelSpan[idx].A > 32)
            {
                img.Dispose();
                return slot;
            }

            img.Dispose();
        }
        return null;
    }
}

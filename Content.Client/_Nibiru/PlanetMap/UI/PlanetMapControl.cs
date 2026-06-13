using Content.Shared._Nibiru.PlanetMap;
using Content.Shared.Maps;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.Utility;
using Robust.Shared.ContentPack;
using Robust.Shared.Input;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Numerics;
using static Robust.Shared.Utility.SpriteSpecifier;
using Color = Robust.Shared.Maths.Color;

namespace Content.Client._Nibiru.PlanetMap.UI;

/// <summary>
/// Scrollable/zoomable control that renders the explored planet map.
/// Looks like a hand-drawn paper map with different colors per tile type.
/// </summary>
public sealed class PlanetMapControl : Control
{
    private const int TilePixels     = 4;   // pixels per tile at zoom 1
    private const int MinZoom        = 1;
    private const int MaxZoom        = 6;
    private const float ScrollSpeed  = 0.15f;

    // ----- Persistent data -----
    private readonly Dictionary<Vector2i, uint[]> _savedChunks = new();
    private readonly Dictionary<Vector2i, uint[]> _savedObjects = new();

    // ----- Zoom / pan state -----
    private int   _zoom   = 2;
    private Vector2 _pan  = Vector2.Zero;   // in tile-space
    private bool  _panning;

    // ----- Player position -----
    public Vector2i PlayerTile;
    public bool     ShowPlayer = true;

    // ----- Colors -----

    // Paper background + ink colors
    private static readonly Color PaperColor    = new Color(0xD4, 0xBF, 0x94, 0xFF);
    private static readonly Color UnexploredColor = new Color(0xBB, 0xA8, 0x80, 0xFF);
    private static readonly Color GridLineColor  = new Color(0x8A, 0x74, 0x50, 0x44);
    private static readonly Color PlayerColor    = new Color(0xEE, 0x22, 0x22, 0xFF);
    private static readonly Color PlayerOutline  = new Color(0xFF, 0xFF, 0xFF, 0xAA);

    // Font for the legend
    private readonly Font _font;
    private readonly IEyeManager _eyeManager;
    private readonly IResourceCache _resCache;
    private readonly IPrototypeManager _proto;
    private readonly Dictionary<string, PlanetMapIconPrototype> _entityIconMap = new();

    // Cache: tile ID -> average color computed from its sprite
    private readonly Dictionary<ushort, Color> _tileColorCache = new();
    private readonly Dictionary<string, Color> _objectColorCache = new();

    // Reverse lookup for exact prototype strings
    private List<string> _objectPrototypes = new();

    public event Action? OnPenPressed;

    public PlanetMapControl()
    {
        var cache = IoCManager.Resolve<IResourceCache>();
        _eyeManager = IoCManager.Resolve<IEyeManager>();
        _resCache   = cache;
        _proto      = IoCManager.Resolve<IPrototypeManager>();
        // Build entity -> icon prototype lookup for quick access when drawing.
        foreach (var icon in _proto.EnumeratePrototypes<PlanetMapIconPrototype>())
        {
            if (icon.Entities == null)
                continue;

            foreach (var ent in icon.Entities)
            {
                if (string.IsNullOrWhiteSpace(ent))
                    continue;

                if (!_entityIconMap.ContainsKey(ent))
                    _entityIconMap[ent] = icon;
            }
        }
        _font     = new VectorFont(cache.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Bold.ttf"), 7);

        RectClipContent = true;
        MouseFilter     = MouseFilterMode.Stop;
        HorizontalExpand = true;
        VerticalExpand   = true;
    }

    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    /// <summary>Load all saved chunks (called when map window opens).</summary>
    public void LoadSavedChunks(Dictionary<Vector2i, uint[]> chunks, Dictionary<Vector2i, uint[]> objects, List<string> objectPrototypes)
    {
        _savedChunks.Clear();
        _savedObjects.Clear();
        foreach (var (k, v) in chunks)
            _savedChunks[k] = v;
        foreach (var (k, v) in objects)
            _savedObjects[k] = v;

        _objectPrototypes = objectPrototypes;
    }

    /// <summary>Merge newly scanned chunks (called after pen-press).</summary>
    public void MergeChunks(Dictionary<Vector2i, uint[]> newChunks, Dictionary<Vector2i, uint[]> newObjects, List<string> objectPrototypes)
    {
        MergeDict(_savedChunks, newChunks);
        MergeDict(_savedObjects, newObjects);

        _objectPrototypes = objectPrototypes;
    }

    private void MergeDict(Dictionary<Vector2i, uint[]> savedMap, Dictionary<Vector2i, uint[]> newMap)
    {
        foreach (var (origin, data) in newMap)
        {
            if (!savedMap.TryGetValue(origin, out var saved))
            {
                saved = new uint[SharedPlanetMapSystem.ArraySize];
                savedMap[origin] = saved;
            }

            for (var i = 0; i < SharedPlanetMapSystem.ArraySize; i++)
            {
                if (data[i] != 0)
                    saved[i] = data[i];
            }
        }
    }

    /// <summary>Centre the view on the player tile.</summary>
    public void CenterOnPlayer()
    {
        _pan = new Vector2(PlayerTile.X, PlayerTile.Y);
    }

    /// <summary>
    /// Try to get the icon prototype for an entity ID.
    /// First tries exact match, then falls back to pattern matching if IdPattern is set.
    /// </summary>
    private PlanetMapIconPrototype? TryGetIconPrototype(string protoId)
    {
        // First try exact match
        if (_entityIconMap.TryGetValue(protoId, out var icon))
            return icon;

        // Fallback: check all prototypes for pattern match
        foreach (var iconProto in _proto.EnumeratePrototypes<PlanetMapIconPrototype>())
        {
            if (iconProto.IdPattern != null && protoId.Contains(iconProto.IdPattern, StringComparison.OrdinalIgnoreCase))
                return iconProto;
        }

        return null;
    }

    // -----------------------------------------------------------------------
    // Input
    // -----------------------------------------------------------------------

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);
        if (args.Function == EngineKeyFunctions.Use)
        {
            _panning      = true;
        }
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        base.KeyBindUp(args);
        if (args.Function == EngineKeyFunctions.Use)
            _panning = false;
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);
        if (!_panning) return;

        var tileSize = TilePixels * _zoom;
        var rel = args.Relative;
        var rot = -(float)_eyeManager.CurrentEye.Rotation.Theta;

        // Inverse rotate the screen delta to world space
        var unrotatedX = rel.X * MathF.Cos(rot) - rel.Y * MathF.Sin(rot);
        var unrotatedY = rel.X * MathF.Sin(rot) + rel.Y * MathF.Cos(rot);

        // Apply to pan. Note that Y is inverted (+ instead of -) because of the visual Y-mirroring in TileToScreen
        _pan.X -= unrotatedX / tileSize;
        _pan.Y += unrotatedY / tileSize;
    }

    protected override void MouseWheel(GUIMouseWheelEventArgs args)
    {
        base.MouseWheel(args);
        _zoom = Math.Clamp(_zoom + (args.Delta.Y > 0 ? 1 : -1), MinZoom, MaxZoom);
    }

    // -----------------------------------------------------------------------
    // Drawing
    // -----------------------------------------------------------------------

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var size      = PixelSize;
        var tileSize  = TilePixels * _zoom;

        // Paper background (drawn by the window, we just clip it here)
        // Draw unexplored placeholder
        handle.DrawRect(new UIBox2(Vector2.Zero, size), UnexploredColor);

        // How many tiles fit in the visible area
        var halfW = (float) size.X / 2f / tileSize;
        var halfH = (float) size.Y / 2f / tileSize;

        var minTileX = (int) MathF.Floor(_pan.X - halfW) - 1;
        var maxTileX = (int) MathF.Ceiling(_pan.X + halfW) + 1;
        var minTileY = (int) MathF.Floor(_pan.Y - halfH) - 1;
        var maxTileY = (int) MathF.Ceiling(_pan.Y + halfH) + 1;

        // Draw tiles
        var camRot = _eyeManager.CurrentEye.Rotation;

        for (var tx = minTileX; tx <= maxTileX; tx++)
        {
            for (var ty = minTileY; ty <= maxTileY; ty++)
            {
                var tileId = GetTileId(tx, ty, _savedChunks);
                var objId  = GetTileId(tx, ty, _savedObjects);

                if (tileId == 0 && objId == 0)
                    continue;

                var screenPos = TileToScreen(tx, ty, size, tileSize, camRot);
                var rect      = new UIBox2(screenPos, screenPos + new Vector2(tileSize, tileSize));

                // 1. Draw floor
                if (tileId != 0)
                {
                    handle.DrawRect(rect, GetTileColor(tileId));
                }

                // 2. Draw object
                if (objId != 0)
                {
                    var index = objId - 1; // 0 is empty
                    if (index >= 0 && index < _objectPrototypes.Count)
                    {
                        var protoId = _objectPrototypes[index];
                        // Shrink manually since UIBox2.Shrunken might not be available
                        var inset = tileSize * 0.2f;
                        var objRect = new UIBox2(rect.Left + inset, rect.Top + inset, rect.Right - inset, rect.Bottom - inset);
                        // If a custom planet map icon prototype exists for this entity prototype, use it.
                        var iconProto = TryGetIconPrototype(protoId);
                        if (iconProto != null)
                        {
                            objRect = objRect.Scale(iconProto.Scale);
                            Color baseColor = Color.White;

                            switch (iconProto.Shape)
                            {
                                case PlanetMapIconShape.Sprite:
                                    // Draw each layer if present. If no layers, fallback to a colored rect.
                                    if (iconProto.Layers != null && iconProto.Layers.Count > 0)
                                    {
                                        foreach (var layer in iconProto.Layers)
                                        {
                                            try
                                            {
                                                // Handle SpriteSpecifier types. Only Texture specifiers can be drawn directly here.
                                                if (layer.Sprite is Robust.Shared.Utility.SpriteSpecifier.Texture texSpec)
                                                {
                                                    var path = texSpec.TexturePath.ToString();
                                                    var tex = _resCache.GetResource<TextureResource>(path).Texture;
                                                    var mod = layer.Tintable ? GetObjectColor(protoId) : layer.Color;
                                                    handle.DrawTextureRectRegion(tex, objRect, null, mod);
                                                }
                                                else
                                                {
                                                    // RSI states are not handled here; fallback to base color for this layer.
                                                    handle.DrawRect(objRect, layer.Color);
                                                }
                                            }
                                            catch
                                            {
                                                // ignore failing layers
                                            }
                                        }
                                    }
                                    else
                                    {
                                        handle.DrawRect(objRect, baseColor);
                                    }
                                    break;

                                case PlanetMapIconShape.Circle:
                                    // Draw a filled circle as an approximation for a semicircle
                                    var c = new Vector2((objRect.Left + objRect.Right) / 2f, (objRect.Top + objRect.Bottom) / 2f);
                                    var r = MathF.Max(1f, (objRect.Right - objRect.Left) / 2f);
                                    handle.DrawCircle(c, r, iconProto.Color);
                                    break;

                                case PlanetMapIconShape.Rectangle:
                                default:
                                    handle.DrawRect(objRect, iconProto.Color);
                                    break;
                            }
                        }
                        else
                        {
                            handle.DrawRect(objRect, GetObjectColor(protoId));
                        }
                    }
                }
            }
        }

        // Grid lines (subtle, only at zoom >= 3)
        if (_zoom >= 3 && tileSize >= 8)
        {
            DrawGridLines(handle, size, tileSize, minTileX, maxTileX, minTileY, maxTileY);
        }

        // Player dot
        if (ShowPlayer)
        {
            var pScreen = TileToScreen(PlayerTile.X, PlayerTile.Y, size, tileSize, camRot) + new Vector2(tileSize / 2f, tileSize / 2f);
            var r       = MathF.Max(3, tileSize / 3f);
            handle.DrawCircle(pScreen, r + 1f, PlayerOutline);
            handle.DrawCircle(pScreen, r,      PlayerColor);
        }
    }

    private bool IsObject(PlanetMapTileType type)
    {
        return type == PlanetMapTileType.Wall || type == PlanetMapTileType.Tree ||
               type == PlanetMapTileType.Flower || type == PlanetMapTileType.Rock ||
               type == PlanetMapTileType.UnknownObj;
    }

    private ushort GetTileId(int tx, int ty, Dictionary<Vector2i, uint[]> dict)
    {
        var chunkOrigin = SharedPlanetMapSystem.GetChunkOrigin(new Vector2i(tx, ty));
        if (!dict.TryGetValue(chunkOrigin, out var data))
            return 0;

        var relative = SharedPlanetMapSystem.GetRelativeTile(new Vector2i(tx, ty), chunkOrigin);
        var index    = SharedPlanetMapSystem.GetTileIndex(relative);

        if (index < 0 || index >= data.Length)
            return 0;

        return (ushort)data[index];
    }

    private Color GetTileColor(ushort tileId)
    {
        if (_tileColorCache.TryGetValue(tileId, out var cached))
            return cached;

        var tileDefManager = IoCManager.Resolve<ITileDefinitionManager>();
        if (tileDefManager.TryGetDefinition(tileId, out var tileDef) && tileDef.Sprite != null)
        {
            var path = tileDef.Sprite.Value.ToString();
            var color = ReadSpriteAverageColor(path);
            if (color != Color.Transparent)
            {
                _tileColorCache[tileId] = color;
                return color;
            }
        }

        // Fallback color based on hash of Tile ID
        int hash = tileId * 1337;
        var fallback = new Color((byte)(50 + hash % 150), (byte)(50 + (hash / 3) % 150), (byte)(50 + (hash / 7) % 150), 255);
        _tileColorCache[tileId] = fallback;
        return fallback;
    }

    private Color GetObjectColor(string protoId)
    {
        if (_objectColorCache.TryGetValue(protoId, out var cached))
            return cached;

        // Fallback color based on string hash
        var hash = (uint)(protoId.GetHashCode() ^ (protoId.GetHashCode() >> 16));
        var color = new Color((byte)(20 + hash % 200), (byte)(20 + (hash >> 4) % 200), (byte)(20 + (hash >> 8) % 200), 255);
        _objectColorCache[protoId] = color;
        return color;
    }

    private readonly Dictionary<string, Color> _spriteColorCache = new();

    private Color ReadSpriteAverageColor(string resPath)
    {
        if (_spriteColorCache.TryGetValue(resPath, out var cached))
            return cached;

        try
        {
            var resMgr = IoCManager.Resolve<IResourceManager>();
            if (resMgr.TryContentFileRead(resPath, out var stream))
            {
                using var img = SixLabors.ImageSharp.Image.Load<Rgba32>(stream);
                long totalR = 0, totalG = 0, totalB = 0, count = 0;

                var pixelSpan = img.GetPixelSpan();
                for (var i = 0; i < pixelSpan.Length; i++)
                {
                    var px = pixelSpan[i];
                    if (px.A < 64) continue;
                    totalR += px.R;
                    totalG += px.G;
                    totalB += px.B;
                    count++;
                }

                if (count > 0)
                {
                    var c = new Color((byte)(totalR / count), (byte)(totalG / count), (byte)(totalB / count), 255);
                    _spriteColorCache[resPath] = c;
                    return c;
                }
            }
        }
        catch
        {
            // Silently ignore
        }

        _spriteColorCache[resPath] = Color.Transparent;
        return Color.Transparent;
    }


    private Vector2 TileToScreen(int tx, int ty, Vector2i screenSize, int tileSize, Angle camRot)
    {
        var centerX = screenSize.X / 2f;
        var centerY = screenSize.Y / 2f;

        var dx =  (tx - _pan.X) * tileSize;
        // Negate dy: world Y increases upward, screen Y increases downward
        var dy = -(ty - _pan.Y) * tileSize;

        // Rotate offset by camera rotation so map matches camera orientation
        var rot  = (float)camRot.Theta;
        var rotX = dx * MathF.Cos(rot) - dy * MathF.Sin(rot);
        var rotY = dx * MathF.Sin(rot) + dy * MathF.Cos(rot);

        return new Vector2(centerX + rotX, centerY + rotY);
    }

    private void DrawGridLines(DrawingHandleScreen handle,
        Vector2i size,
        int tileSize,
        int minTX, int maxTX, int minTY, int maxTY)
    {
        var centerX = size.X / 2f;
        var centerY = size.Y / 2f;
        var camRot = _eyeManager.CurrentEye.Rotation;
        var rot = (float)camRot.Theta;
        var cosRot = MathF.Cos(rot);
        var sinRot = MathF.Sin(rot);

        // Draw vertical lines (constant X)
        for (var tx = minTX; tx <= maxTX; tx++)
        {
            var screenX1 = TileToScreen(tx, minTY, size, tileSize, camRot).X;
            var screenY1 = TileToScreen(tx, minTY, size, tileSize, camRot).Y;
            var screenX2 = TileToScreen(tx, maxTY, size, tileSize, camRot).X;
            var screenY2 = TileToScreen(tx, maxTY, size, tileSize, camRot).Y;

            handle.DrawLine(new Vector2(screenX1, screenY1), new Vector2(screenX2, screenY2), GridLineColor);
        }

        // Draw horizontal lines (constant Y)
        for (var ty = minTY; ty <= maxTY; ty++)
        {
            var screenX1 = TileToScreen(minTX, ty, size, tileSize, camRot).X;
            var screenY1 = TileToScreen(minTX, ty, size, tileSize, camRot).Y;
            var screenX2 = TileToScreen(maxTX, ty, size, tileSize, camRot).X;
            var screenY2 = TileToScreen(maxTX, ty, size, tileSize, camRot).Y;

            handle.DrawLine(new Vector2(screenX1, screenY1), new Vector2(screenX2, screenY2), GridLineColor);
        }
    }

}

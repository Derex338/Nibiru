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
using Robust.Shared.Timing;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Numerics;
using Color = Robust.Shared.Maths.Color;

namespace Content.Client._Nibiru.PlanetMap.UI;

/// <summary>
/// Scrollable/zoomable control that renders the explored planet map.
/// </summary>
public sealed class PlanetMapControl : Control
{
    // Pixels per tile at zoom level 1
    private const int TilePixels = 4;
    private const int MinZoom    = 1;
    private const int MaxZoom    = 6;

    // -----------------------------------------------------------------------
    // Persistent chunk data (merges from network batches)
    // -----------------------------------------------------------------------
    private readonly Dictionary<Vector2i, uint[]> _savedChunks  = new();
    private readonly Dictionary<Vector2i, uint[]> _savedObjects = new();

    // -----------------------------------------------------------------------
    // Camera state
    // -----------------------------------------------------------------------
    private int     _zoom    = 2;
    private Vector2 _pan     = Vector2.Zero; // tile-space centre of viewport
    private bool    _panning;

    // -----------------------------------------------------------------------
    // Player marker
    // -----------------------------------------------------------------------
    public Vector2i PlayerTile;
    public bool     ShowPlayer = true;

    // -----------------------------------------------------------------------
    // Colour palette
    // -----------------------------------------------------------------------
    private static readonly Color UnexploredColor = new(0xBB, 0xA8, 0x80, 0xFF);
    private static readonly Color GridLineColor   = new(0x8A, 0x74, 0x50, 0x55);
    private static readonly Color PlayerColor     = new(0xEE, 0x22, 0x22, 0xFF);
    private static readonly Color PlayerOutline   = new(0xFF, 0xFF, 0xFF, 0xAA);
    private static readonly Color CompassNorth    = new(0xCC, 0x22, 0x22, 0xFF);
    private static readonly Color CompassBg       = new(0x22, 0x1A, 0x10, 0xCC);
    private static readonly Color HudTextColor    = new(1f, 1f, 1f, 0.65f);

    // -----------------------------------------------------------------------
    // Services (cached at construction, not resolved per-frame)
    // -----------------------------------------------------------------------
    private readonly Font                    _font;
    private readonly IEyeManager            _eyeManager;
    private readonly IResourceCache         _resCache;
    private readonly IPrototypeManager      _proto;
    private readonly ITileDefinitionManager _tileDefManager; // was resolved per-tile in original
    private readonly IResourceManager       _resMgr;        // was resolved per-sprite in original
    private readonly IGameTiming            _gameTiming;

    // -----------------------------------------------------------------------
    // Icon prototype lookup (O(1) resolved, cached per prototype ID)
    // -----------------------------------------------------------------------

    // Exact entity-ID → icon prototype (built at startup)
    private readonly Dictionary<string, PlanetMapIconPrototype> _entityIconMap = new();

    // Pattern-based fallback list (built at startup, avoids EnumeratePrototypes per draw)
    private readonly List<(string Pattern, PlanetMapIconPrototype Proto)> _patternIconList = new();

    // Cache: prototype ID → resolved icon (null = no icon, avoids re-searching)
    private readonly Dictionary<string, PlanetMapIconPrototype?> _resolvedIconCache = new();

    // -----------------------------------------------------------------------
    // Colour caches
    // -----------------------------------------------------------------------
    private readonly Dictionary<ushort, Color> _tileColorCache   = new();
    private readonly Dictionary<string, Color> _objectColorCache = new();
    private readonly Dictionary<string, Color> _spriteColorCache = new();

    private List<string> _objectPrototypes = new();

    public event Action? OnPenPressed;

    // -----------------------------------------------------------------------
    // Constructor
    // -----------------------------------------------------------------------

    public PlanetMapControl()
    {
        _eyeManager     = IoCManager.Resolve<IEyeManager>();
        _resCache       = IoCManager.Resolve<IResourceCache>();
        _proto          = IoCManager.Resolve<IPrototypeManager>();
        _tileDefManager = IoCManager.Resolve<ITileDefinitionManager>();
        _resMgr         = IoCManager.Resolve<IResourceManager>();
        _gameTiming     = IoCManager.Resolve<IGameTiming>();

        // Build icon lookup tables once — O(n prototypes), done at startup only
        foreach (var icon in _proto.EnumeratePrototypes<PlanetMapIconPrototype>())
        {
            if (icon.Entities != null)
            {
                foreach (var ent in icon.Entities)
                {
                    if (!string.IsNullOrWhiteSpace(ent) && !_entityIconMap.ContainsKey(ent))
                        _entityIconMap[ent] = icon;
                }
            }
            if (icon.IdPattern != null)
                _patternIconList.Add((icon.IdPattern, icon));
        }

        _font = new VectorFont(
            _resCache.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Bold.ttf"), 7);

        RectClipContent  = true;
        MouseFilter      = MouseFilterMode.Stop;
        HorizontalExpand = true;
        VerticalExpand   = true;
    }

    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    /// <summary>Clears all saved chunk data.</summary>
    public void ClearChunks()
    {
        _savedChunks.Clear();
        _savedObjects.Clear();
    }

    /// <summary>Replaces all saved data (clear + merge).</summary>
    public void LoadSavedChunks(
        Dictionary<Vector2i, uint[]> chunks,
        Dictionary<Vector2i, uint[]> objects,
        List<string>                 objectPrototypes)
    {
        ClearChunks();
        MergeChunks(chunks, objects, objectPrototypes);
    }

    /// <summary>Merges incoming chunk data into the saved map (non-zero values win).</summary>
    public void MergeChunks(
        Dictionary<Vector2i, uint[]> newChunks,
        Dictionary<Vector2i, uint[]> newObjects,
        List<string>                 objectPrototypes)
    {
        MergeDict(_savedChunks,  newChunks);
        MergeDict(_savedObjects, newObjects);
        _objectPrototypes = objectPrototypes;
    }

    private static void MergeDict(Dictionary<Vector2i, uint[]> saved, Dictionary<Vector2i, uint[]> incoming)
    {
        foreach (var (origin, data) in incoming)
        {
            if (!saved.TryGetValue(origin, out var existing))
            {
                existing = new uint[SharedPlanetMapSystem.ArraySize];
                saved[origin] = existing;
            }
            for (var i = 0; i < SharedPlanetMapSystem.ArraySize; i++)
            {
                if (data[i] != 0) existing[i] = data[i];
            }
        }
    }

    /// <summary>Centres the view on the player tile.</summary>
    public void CenterOnPlayer()
    {
        _pan = new Vector2(PlayerTile.X, PlayerTile.Y);
    }

    // -----------------------------------------------------------------------
    // Icon prototype lookup (with full caching)
    // -----------------------------------------------------------------------

    private PlanetMapIconPrototype? TryGetIconPrototype(string protoId)
    {
        // Already resolved (result may be null = "no icon")
        if (_resolvedIconCache.TryGetValue(protoId, out var cached))
            return cached;

        // Exact match
        if (_entityIconMap.TryGetValue(protoId, out var icon))
        {
            _resolvedIconCache[protoId] = icon;
            return icon;
        }

        // Pattern fallback — iterates pre-built list, no allocations
        foreach (var (pattern, proto) in _patternIconList)
        {
            if (protoId.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                _resolvedIconCache[protoId] = proto;
                return proto;
            }
        }

        _resolvedIconCache[protoId] = null;
        return null;
    }

    // -----------------------------------------------------------------------
    // Input
    // -----------------------------------------------------------------------

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);
        if (args.Function == EngineKeyFunctions.Use)
            _panning = true;
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

        var tileSize    = TilePixels * _zoom;
        var rel         = args.Relative;
        // Инвертируем вращение для перемещения мыши в соответствии с новой ориентацией
        var rot         = (float)_eyeManager.CurrentEye.Rotation.Theta;
        var unrotatedX  = rel.X * MathF.Cos(rot) - rel.Y * MathF.Sin(rot);
        var unrotatedY  = rel.X * MathF.Sin(rot) + rel.Y * MathF.Cos(rot);

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

        var size     = PixelSize;
        var tileSize = TilePixels * _zoom;
        // Инвертируем угол поворота камеры, чтобы карта вращалась в правильную сторону
        var camRot   = -_eyeManager.CurrentEye.Rotation;

        // Unexplored background
        handle.DrawRect(new UIBox2(Vector2.Zero, size), UnexploredColor);

        // --- Compute visible tile range ---
        var halfW = size.X / 2f / tileSize;
        var halfH = size.Y / 2f / tileSize;

        var minTileX = (int)MathF.Floor(_pan.X - halfW) - 1;
        var maxTileX = (int)MathF.Ceiling(_pan.X + halfW) + 1;
        var minTileY = (int)MathF.Floor(_pan.Y - halfH) - 1;
        var maxTileY = (int)MathF.Ceiling(_pan.Y + halfH) + 1;

        // --- Chunk-level culling: compute chunk coordinate bounds of viewport ---
        // Adding +/−1 ensures we never clip chunks at the edges.
        var cSize    = SharedPlanetMapSystem.ChunkSize;
        var minCX    = (int)MathF.Floor((float)minTileX / cSize) - 1;
        var maxCX    = (int)MathF.Ceiling((float)maxTileX / cSize) + 1;
        var minCY    = (int)MathF.Floor((float)minTileY / cSize) - 1;
        var maxCY    = (int)MathF.Ceiling((float)maxTileY / cSize) + 1;

        // --- Draw visible chunks ---
        // We iterate _savedChunks (the dictionary of loaded data) instead of the
        // entire viewport range. For 1000 chunks, only those overlapping the viewport
        // (typically 20–50) pass the coordinate check. Inside each chunk we use direct
        // array indexing — no dictionary lookup per tile.
        foreach (var (chunkOrigin, chunkData) in _savedChunks)
        {
            // Chunk culling: skip chunks entirely outside viewport
            if (chunkOrigin.X < minCX || chunkOrigin.X > maxCX) continue;
            if (chunkOrigin.Y < minCY || chunkOrigin.Y > maxCY) continue;

            _savedObjects.TryGetValue(chunkOrigin, out var objData);

            var baseX = chunkOrigin.X * cSize;
            var baseY = chunkOrigin.Y * cSize;

            for (var lx = 0; lx < cSize; lx++)
            {
                var tx = baseX + lx;
                if (tx < minTileX || tx > maxTileX) continue;

                for (var ly = 0; ly < cSize; ly++)
                {
                    var ty = baseY + ly;
                    if (ty < minTileY || ty > maxTileY) continue;

                    // Direct array access — O(1), no dictionary lookup
                    var idx    = lx * cSize + ly;
                    var tileId = (ushort)chunkData[idx];
                    var objId  = objData != null ? objData[idx] : 0u;

                    if (tileId == 0 && objId == 0) continue;

                    var screenPos = TileToScreen(tx, ty, size, tileSize, camRot);
                    var rect      = new UIBox2(screenPos, screenPos + new Vector2(tileSize, tileSize));

                    // 1. Floor tile (with subtle position-hash variation for visual texture)
                    if (tileId != 0)
                    {
                        var tileColor = GetTileColor(tileId);
                        // Micro-variation: darken/lighten slightly based on position
                        // Creates a subtle texture without extra textures
                        var variation = ((tx * 7 + ty * 13) & 0x1F) / 255f * 0.08f - 0.04f;
                        var varColor = new Color(
                            Math.Clamp(tileColor.R + variation, 0f, 1f),
                            Math.Clamp(tileColor.G + variation, 0f, 1f),
                            Math.Clamp(tileColor.B + variation, 0f, 1f),
                            1f);
                        handle.DrawRect(rect, varColor);
                    }

                    // 2. Object overlay
                    if (objId != 0)
                        DrawObject(handle, rect, objId, tileSize);
                }
            }
        }

        // Grid lines (only at higher zoom to avoid visual noise)
        if (_zoom >= 3 && tileSize >= 8)
            DrawGridLines(handle, size, tileSize, minTileX, maxTileX, minTileY, maxTileY, camRot);

        // Player marker with pulsing animation
        if (ShowPlayer)
            DrawPlayerMarker(handle, size, tileSize, camRot);

        // Compass rose (top-right)
        // Компас вращается противоположно направлению поворота карты (т.е. по оригинальному углу камеры)
        DrawCompass(handle, size, -camRot);

        // HUD: zoom + player coordinates (bottom-left)
        DrawHud(handle, size);
    }

    // -----------------------------------------------------------------------
    // Draw helpers
    // -----------------------------------------------------------------------

    private void DrawObject(DrawingHandleScreen handle, UIBox2 rect, uint objId, int tileSize)
    {
        var index = (int)(objId - 1);
        if (index < 0 || index >= _objectPrototypes.Count) return;

        var protoId   = _objectPrototypes[index];
        var inset     = tileSize * 0.2f;
        var objRect   = new UIBox2(rect.Left + inset, rect.Top + inset, rect.Right - inset, rect.Bottom - inset);
        var iconProto = TryGetIconPrototype(protoId);

        if (iconProto != null)
        {
            objRect = objRect.Scale(iconProto.Scale);
            switch (iconProto.Shape)
            {
                case PlanetMapIconShape.Sprite:
                    if (iconProto.Layers is { Count: > 0 })
                    {
                        foreach (var layer in iconProto.Layers)
                        {
                            try
                            {
                                if (layer.Sprite is Robust.Shared.Utility.SpriteSpecifier.Texture texSpec)
                                {
                                    var tex = _resCache.GetResource<TextureResource>(
                                        texSpec.TexturePath.ToString()).Texture;
                                    var mod = layer.Tintable ? GetObjectColor(protoId) : layer.Color;
                                    handle.DrawTextureRectRegion(tex, objRect, null, mod);
                                }
                                else
                                {
                                    handle.DrawRect(objRect, layer.Color);
                                }
                            }
                            catch { /* ignore failing layers */ }
                        }
                    }
                    else
                    {
                        handle.DrawRect(objRect, Color.White);
                    }
                    break;

                case PlanetMapIconShape.Circle:
                    var c = new Vector2(
                        (objRect.Left + objRect.Right)   / 2f,
                        (objRect.Top  + objRect.Bottom)  / 2f);
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

    private void DrawPlayerMarker(DrawingHandleScreen handle, Vector2i size, int tileSize, Angle camRot)
    {
        var center = TileToScreen(PlayerTile.X, PlayerTile.Y, size, tileSize, camRot)
                     + new Vector2(tileSize / 2f, tileSize / 2f);
        var r = MathF.Max(3f, tileSize / 3f);

        // Animated pulsing ring (uses game time for smooth animation)
        var t     = (float)(_gameTiming.CurTime.TotalSeconds % 1.6) / 1.6f;
        var pulse = MathF.Sin(t * MathF.PI); // 0 → 1 → 0
        handle.DrawCircle(center, r + 3f + pulse * 6f,
            PlayerColor.WithAlpha(pulse * 0.45f));

        // Outline ring
        handle.DrawCircle(center, r + 1.5f, PlayerOutline);
        // Main filled dot
        handle.DrawCircle(center, r, PlayerColor);
        // Small highlight
        handle.DrawCircle(center, r * 0.3f, new Color(1f, 0.85f, 0.85f, 0.85f));
    }

    private void DrawCompass(DrawingHandleScreen handle, Vector2i size, Angle camRot)
    {
        const float R      = 18f;
        const float Margin = 14f;
        var center = new Vector2(size.X - Margin - R, Margin + R);

        // Shadow
        handle.DrawCircle(center, R + 3f, new Color(0f, 0f, 0f, 0.3f));
        // Background
        handle.DrawCircle(center, R, CompassBg);

        var rot = (float)camRot.Theta;

        // North/south axis
        var north = new Vector2( MathF.Sin(rot), -MathF.Cos(rot));
        var south = -north;
        var east  = new Vector2( MathF.Cos(rot),  MathF.Sin(rot));
        var west  = -east;

        var northTip = center + north * (R - 4f);
        var southTip = center + south * (R - 4f);
        var eastTip  = center + east  * (R - 6f);
        var westTip  = center + west  * (R - 6f);

        // Cardinal lines (grey for E/W, grey for S)
        handle.DrawLine(center, southTip, new Color(0.7f, 0.7f, 0.7f, 0.7f));
        handle.DrawLine(center, eastTip,  new Color(0.7f, 0.7f, 0.7f, 0.5f));
        handle.DrawLine(center, westTip,  new Color(0.7f, 0.7f, 0.7f, 0.5f));

        // North arm (red — most prominent)
        handle.DrawLine(center, northTip, CompassNorth);

        // Tiny arrowhead for north: two short lines angled back
        var perp = new Vector2(-north.Y, north.X) * 3.5f;
        var back = center + north * (R - 10f);
        handle.DrawLine(northTip, back + perp,  CompassNorth);
        handle.DrawLine(northTip, back - perp,  CompassNorth);

        // Centre pivot
        handle.DrawCircle(center, 2.5f, new Color(1f, 1f, 1f, 0.9f));
    }

    private void DrawHud(DrawingHandleScreen handle, Vector2i size)
    {
        const float Margin = 7f;
        const float LineH  = 12f;

        // Player coordinates
        var coordsText = $"Pos: {PlayerTile.X}, {PlayerTile.Y}";
        handle.DrawString(_font, new Vector2(Margin, size.Y - Margin - LineH),
            coordsText, HudTextColor);

        // Zoom
        var zoomText = $"Zoom: {_zoom}x";
        handle.DrawString(_font, new Vector2(Margin, size.Y - Margin - LineH * 2 - 2f),
            zoomText, HudTextColor);
    }

    // -----------------------------------------------------------------------
    // Colour helpers
    // -----------------------------------------------------------------------

    private Color GetTileColor(ushort tileId)
    {
        if (_tileColorCache.TryGetValue(tileId, out var cached))
            return cached;

        // Try to read average colour from the tile sprite
        if (_tileDefManager.TryGetDefinition(tileId, out var tileDef) && tileDef.Sprite != null)
        {
            var path  = tileDef.Sprite.Value.ToString();
            var color = ReadSpriteAverageColor(path);
            if (color != Color.Transparent)
            {
                _tileColorCache[tileId] = color;
                return color;
            }
        }

        // Deterministic fallback based on ID
        int hash     = tileId * 1337;
        var fallback = new Color(
            (byte)(50 + hash       % 150),
            (byte)(50 + (hash / 3) % 150),
            (byte)(50 + (hash / 7) % 150),
            255);
        _tileColorCache[tileId] = fallback;
        return fallback;
    }

    private Color GetObjectColor(string protoId)
    {
        if (_objectColorCache.TryGetValue(protoId, out var cached))
            return cached;

        var hash  = (uint)(protoId.GetHashCode() ^ (protoId.GetHashCode() >> 16));
        var color = new Color(
            (byte)(20 + hash          % 200),
            (byte)(20 + (hash >>  4)  % 200),
            (byte)(20 + (hash >>  8)  % 200),
            255);
        _objectColorCache[protoId] = color;
        return color;
    }

    private Color ReadSpriteAverageColor(string resPath)
    {
        if (_spriteColorCache.TryGetValue(resPath, out var cached))
            return cached;

        try
        {
            if (_resMgr.TryContentFileRead(resPath, out var stream))
            {
                using var img = SixLabors.ImageSharp.Image.Load<Rgba32>(stream);
                long totalR = 0, totalG = 0, totalB = 0, count = 0;
                var pixels = img.GetPixelSpan();
                for (var i = 0; i < pixels.Length; i++)
                {
                    var px = pixels[i];
                    if (px.A < 64) continue;
                    totalR += px.R;
                    totalG += px.G;
                    totalB += px.B;
                    count++;
                }
                if (count > 0)
                {
                    var c = new Color(
                        (byte)(totalR / count),
                        (byte)(totalG / count),
                        (byte)(totalB / count),
                        255);
                    _spriteColorCache[resPath] = c;
                    return c;
                }
            }
        }
        catch { /* silently ignore missing / invalid sprites */ }

        _spriteColorCache[resPath] = Color.Transparent;
        return Color.Transparent;
    }

    // -----------------------------------------------------------------------
    // Geometry
    // -----------------------------------------------------------------------

    private Vector2 TileToScreen(int tx, int ty, Vector2i screenSize, int tileSize, Angle camRot)
    {
        var cx = screenSize.X / 2f;
        var cy = screenSize.Y / 2f;

        // World-to-screen offset (Y negated because world Y↑ = screen Y↓)
        var dx =  (tx - _pan.X) * tileSize;
        var dy = -(ty - _pan.Y) * tileSize;

        // Rotate by camera angle
        var rot  = (float)camRot.Theta;
        var cosR = MathF.Cos(rot);
        var sinR = MathF.Sin(rot);

        return new Vector2(cx + dx * cosR - dy * sinR,
                           cy + dx * sinR + dy * cosR);
    }

    private void DrawGridLines(DrawingHandleScreen handle,
        Vector2i size, int tileSize,
        int minTX, int maxTX, int minTY, int maxTY,
        Angle camRot)
    {
        for (var tx = minTX; tx <= maxTX; tx++)
        {
            var p1 = TileToScreen(tx, minTY, size, tileSize, camRot);
            var p2 = TileToScreen(tx, maxTY, size, tileSize, camRot);
            handle.DrawLine(p1, p2, GridLineColor);
        }
        for (var ty = minTY; ty <= maxTY; ty++)
        {
            var p1 = TileToScreen(minTX, ty, size, tileSize, camRot);
            var p2 = TileToScreen(maxTX, ty, size, tileSize, camRot);
            handle.DrawLine(p1, p2, GridLineColor);
        }
    }
}

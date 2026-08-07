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
    private readonly Dictionary<Vector2i, uint[]> _savedZones   = new();

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
    private List<string> _zonePrototypes   = new();

    // Resolved zone visuals (prototype ID → cached render data)
    private readonly Dictionary<string, PlanetMapZonePrototype?> _resolvedZoneCache = new();
    private readonly Dictionary<string, Texture>                  _zoneTextureCache  = new();


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
        _savedZones.Clear();
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

    /// <summary>
    /// Merges incoming chunk data into the saved map. When <paramref name="overwriteTiles"/> is
    /// provided (scan results), only those tiles are overwritten (including zeroing, so removed
    /// objects disappear); tiles in the same chunk not part of that scan are preserved. When null
    /// (initial open-stream), everything overwrites.
    /// </summary>
    public void MergeChunks(
        Dictionary<Vector2i, uint[]> newChunks,
        Dictionary<Vector2i, uint[]> newObjects,
        List<string>                 objectPrototypes,
        Dictionary<Vector2i, uint[]>? newZones = null,
        List<string>?                zonePrototypes = null,
        HashSet<Vector2i>?           overwriteTiles = null)
    {
        MergeDict(_savedChunks,  newChunks,  overwriteTiles);
        MergeDict(_savedObjects, newObjects, overwriteTiles);
        if (newZones != null)
            MergeDict(_savedZones, newZones, overwriteTiles);
        if (zonePrototypes != null)
            _zonePrototypes = zonePrototypes;
        _objectPrototypes = objectPrototypes;
    }

    private static void MergeDict(
        Dictionary<Vector2i, uint[]> saved,
        Dictionary<Vector2i, uint[]> incoming,
        HashSet<Vector2i>? overwriteTiles)
    {
        foreach (var (origin, data) in incoming)
        {
            if (!saved.TryGetValue(origin, out var existing))
            {
                existing = new uint[SharedPlanetMapSystem.ArraySize];
                saved[origin] = existing;
            }

            if (overwriteTiles == null)
            {
                // Full overwrite (initial open-stream).
                data.CopyTo(existing, 0);
                continue;
            }

            // Scan result: only re-classified tiles are overwritten (incl. zeroing).
            var baseX = origin.X * SharedPlanetMapSystem.ChunkSize;
            var baseY = origin.Y * SharedPlanetMapSystem.ChunkSize;
            for (var lx = 0; lx < SharedPlanetMapSystem.ChunkSize; lx++)
            for (var ly = 0; ly < SharedPlanetMapSystem.ChunkSize; ly++)
            {
                if (!overwriteTiles.Contains(new Vector2i(baseX + lx, baseY + ly)))
                    continue;
                existing[lx * SharedPlanetMapSystem.ChunkSize + ly] =
                    data[lx * SharedPlanetMapSystem.ChunkSize + ly];
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

                    // 1. Floor tile (intensity variation for visual texture)
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
                }
            }
        }

        // Zone blobs (forests, etc.) — under the object icons
        DrawZones(handle, size, tileSize, camRot, minTileX, maxTileX, minTileY, maxTileY);

        // 2. Object overlay (skipping tiles covered by a dense zone)
        foreach (var (chunkOrigin, chunkData) in _savedChunks)
        {
            if (chunkOrigin.X < minCX || chunkOrigin.X > maxCX) continue;
            if (chunkOrigin.Y < minCY || chunkOrigin.Y > maxCY) continue;

            _savedObjects.TryGetValue(chunkOrigin, out var objData);
            _savedZones.TryGetValue(chunkOrigin, out var zoneData);

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

                    var idx   = lx * cSize + ly;
                    var objId = objData != null ? objData[idx] : 0u;
                    if (objId == 0) continue;

                    // Dense zone members are drawn by the blob; skip their icons
                    if (zoneData != null && zoneData[idx] != 0) continue;

                    var screenPos = TileToScreen(tx, ty, size, tileSize, camRot);
                    var rect      = new UIBox2(screenPos, screenPos + new Vector2(tileSize, tileSize));
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

    // -----------------------------------------------------------------------
    // Zone blob rendering
    //
    // Each zone member is drawn as a soft-edged disc in screen space. Overlapping discs of a
    // dense cluster merge into one smooth blob, with round (not spiky) edges. No marching-squares
    // polygon tessellation is used, avoiding seams and spikes entirely.
    // -----------------------------------------------------------------------

    // Base disc radius (tiles) around each zone member. Small enough to stay within the member's
    // own tile — isolated members render as a single small dot. Members that have a neighbour
    // within the zone's <radius> grow a bit larger so dense clusters read as one connected blob.
    private const float ZoneBlobRadius = 0.5f;
    private const int   ZoneDiscSegments = 20;
    private const int   ZoneViewPad = 4; // tiles of padding beyond the viewport when culling members

    private void DrawZones(
        DrawingHandleScreen handle,
        Vector2i            size,
        int                 tileSize,
        Angle               camRot,
        int                 minTileX, int maxTileX,
        int                 minTileY, int maxTileY)
    {
        if (_zonePrototypes == null || _savedZones.Count == 0)
            return;

        // Collect all zone members in view, grouped by zone id.
        var perZone = new Dictionary<int, List<Vector2i>>();
        var cSize   = SharedPlanetMapSystem.ChunkSize;
        foreach (var (chunkOrigin, zoneData) in _savedZones)
        {
            var baseX = chunkOrigin.X * cSize;
            var baseY = chunkOrigin.Y * cSize;

            for (var lx = 0; lx < cSize; lx++)
            {
                var tx = baseX + lx;
                if (tx < minTileX - ZoneViewPad || tx > maxTileX + ZoneViewPad) continue;
                for (var ly = 0; ly < cSize; ly++)
                {
                    var idx = lx * cSize + ly;
                    var z = (int) zoneData[idx];
                    if (z == 0) continue;
                    var ty = baseY + ly;
                    if (ty < minTileY - ZoneViewPad || ty > maxTileY + ZoneViewPad) continue;
                    if (!perZone.TryGetValue(z, out var list))
                    {
                        list = new List<Vector2i>();
                        perZone[z] = list;
                    }
                    list.Add(new Vector2i(tx, ty));
                }
            }
        }

        foreach (var (zoneIdx, members) in perZone)
        {
            var proto = ResolveZonePrototype(zoneIdx);
            if (proto == null) continue;
            var color = GetZoneColor(proto);
            var tex   = GetZoneTexture(proto);
            var repeat = proto.TextureRepeatScale;

            // Per-member disc radius: base stays within the member's tile; a member that has a
            // neighbour close by (dense cluster) gets a slightly larger disc so the cluster reads
            // as one connected blob, while isolated members stay a single small dot.
            var clusterDist = proto.Radius;
            var clusterR    = MathF.Max(ZoneBlobRadius, MathF.Min(1.4f, clusterDist * 0.45f));

            // Spatial grid for fast neighbour lookups.
            var grid = new Dictionary<Vector2i, List<Vector2i>>();
            foreach (var m in members)
            {
                var cell = new Vector2i(
                    (int) MathF.Floor(m.X / (float) clusterDist),
                    (int) MathF.Floor(m.Y / (float) clusterDist));
                if (!grid.TryGetValue(cell, out var list))
                {
                    list = new List<Vector2i>();
                    grid[cell] = list;
                }
                list.Add(m);
            }

            foreach (var m in members)
            {
                var hasNeighbour = false;
                var cell = new Vector2i(
                    (int) MathF.Floor(m.X / (float) clusterDist),
                    (int) MathF.Floor(m.Y / (float) clusterDist));
                var distSq = clusterDist * clusterDist;
                for (var dx = -1; dx <= 1 && !hasNeighbour; dx++)
                for (var dy = -1; dy <= 1 && !hasNeighbour; dy++)
                {
                    if (!grid.TryGetValue(cell + new Vector2i(dx, dy), out var other))
                        continue;
                    foreach (var o in other)
                    {
                        if (o == m) continue;
                        var ddx = m.X - o.X;
                        var ddy = m.Y - o.Y;
                        if (ddx * ddx + ddy * ddy <= distSq)
                        {
                            hasNeighbour = true;
                            break;
                        }
                    }
                }

                DrawZoneDisc(handle, size, tileSize, camRot, m,
                    hasNeighbour ? clusterR : ZoneBlobRadius, color, tex, repeat);
            }
        }
    }

    /// <summary>
    /// Draws one zone disc as a triangle fan built in TILE space, then projected to screen.
    /// Each vertex carries its own UV (tile coord / repeat), so a texture tiles continuously
    /// across overlapping discs of a cluster instead of being stretched over a single disc.
    /// Opaque fill: overlapping discs of the same zone don't darken at their seams.
    /// </summary>
    private void DrawZoneDisc(
        DrawingHandleScreen handle, Vector2i size, int tileSize, Angle camRot,
        Vector2i centerTile, float radiusTiles, Color color, Texture? tex, float repeat)
    {
        var solid = color.WithAlpha(1f);

        // Fan vertices in tile space (center + rim points around the disc).
        var fan = new Vector2[ZoneDiscSegments + 2];
        fan[0] = new Vector2(centerTile.X, centerTile.Y);
        for (var i = 0; i <= ZoneDiscSegments; i++)
        {
            var a = MathF.Tau * i / ZoneDiscSegments;
            fan[i + 1] = new Vector2(
                centerTile.X + MathF.Cos(a) * radiusTiles,
                centerTile.Y + MathF.Sin(a) * radiusTiles);
        }

        if (tex == null)
        {
            // Project rim to screen and fill the polygon.
            var pts = new Vector2[fan.Length];
            for (var i = 0; i < fan.Length; i++)
                pts[i] = TileToScreen(fan[i], size, tileSize, camRot);
            handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, pts, solid);
            return;
        }

        // Textured: project each tile-space vertex to screen AND keep its tile UV.
        var uVerts = new DrawVertexUV2D[fan.Length];
        for (var i = 0; i < fan.Length; i++)
        {
            var t = fan[i];
            var screen = TileToScreen(t, size, tileSize, camRot);
            uVerts[i] = new DrawVertexUV2D(screen, new Vector2(t.X / repeat, t.Y / repeat));
        }
        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, tex, uVerts, solid);
    }


    private PlanetMapZonePrototype? ResolveZonePrototype(int zoneIdx)
    {
        var idx = zoneIdx - 1;
        if (idx < 0 || idx >= _zonePrototypes.Count)
            return null;
        var id = _zonePrototypes[idx];
        if (id == null) return null;

        if (_resolvedZoneCache.TryGetValue(id, out var cached))
            return cached;

        PlanetMapZonePrototype? proto = null;
        _proto.TryIndex(id, out proto);
        _resolvedZoneCache[id] = proto;
        return proto;
    }

    private Color GetZoneColor(PlanetMapZonePrototype proto)
    {
        return proto.Color.WithAlpha(proto.Alpha);
    }

    private Texture? GetZoneTexture(PlanetMapZonePrototype proto)
    {
        if (proto.Sprite is not Robust.Shared.Utility.SpriteSpecifier.Texture texSpec)
            return null;
        var path = texSpec.TexturePath.ToString();
        if (_zoneTextureCache.TryGetValue(path, out var cached))
            return cached;
        try
        {
            var tex = _resCache.GetResource<TextureResource>(path).Texture;
            _zoneTextureCache[path] = tex;
            return tex;
        }
        catch { return null; }
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
        => TileToScreen(new Vector2(tx, ty), screenSize, tileSize, camRot);

    /// <summary>Tile → screen with fractional tile coordinates.</summary>
    private Vector2 TileToScreen(Vector2 tile, Vector2i screenSize, int tileSize, Angle camRot)
    {
        var cx = screenSize.X / 2f;
        var cy = screenSize.Y / 2f;

        // World-to-screen offset (Y negated because world Y↑ = screen Y↓)
        var dx =  (tile.X - _pan.X) * tileSize;
        var dy = -(tile.Y - _pan.Y) * tileSize;

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


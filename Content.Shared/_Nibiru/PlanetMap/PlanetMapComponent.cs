using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Nibiru.PlanetMap;

/// <summary>
/// Component placed on the "planet map" item entity.
/// Stores the persistent explored tile data (saved with the entity).
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class PlanetMapComponent : Component
{
    /// <summary>
    /// How many tiles make up one chunk side.
    /// Must match <see cref="SharedPlanetMapSystem.ChunkSize"/>.
    /// </summary>
    public const int ChunkSize = 8;

    /// <summary>
    /// Persistent map data: chunk origin → flat array of packed uint (tile TypeId).
    /// Saved as a DataField so it persists across saves.
    /// </summary>
    [DataField("savedChunks")]
    public Dictionary<Vector2i, uint[]> SavedChunks = new();

    /// <summary>
    /// Persistent map data for objects (walls, trees, etc.) on top of the tiles.
    /// Values are indices into <see cref="ObjectPrototypes"/> (1-based; 0 = empty).
    /// </summary>
    [DataField("savedObjects")]
    public Dictionary<Vector2i, uint[]> SavedObjects = new();

    /// <summary>
    /// Registry of entity prototype IDs used in <see cref="SavedObjects"/>.
    /// </summary>
    [DataField("objectPrototypes")]
    public List<string> ObjectPrototypes = new();

    /// <summary>
    /// Scan radius in tiles (how far from the player to scan on each pen-press).
    /// </summary>
    [DataField]
    public int ScanRadius = 24;

    /// <summary>
    /// If true, we only record tiles that are visible (not obstructed) from the player.
    /// </summary>
    [DataField]
    public bool RequireVisibility = true;

    /// <summary>
    /// Number of tiles to classify (via ClassifyTile) per server tick during an active scan job.
    /// Higher = faster scan completion, higher server frametime spike per tick.
    /// At 60 UPS, 512 tiles/tick → ~3 ticks for ScanRadius=24 (~1800 tiles).
    /// </summary>
    [DataField]
    public int StreamingBatchSize = 512;

    /// <summary>
    /// Number of chunks to send to the client per server tick when streaming saved map data
    /// after the player opens the map UI.
    /// Lower = smoother, but takes more ticks to fully deliver 1000+ chunks.
    /// </summary>
    [DataField]
    public int OpenBatchSize = 30;
}

/// <summary>
/// Shared constants/utilities for planet map chunk math.
/// </summary>
public static class SharedPlanetMapSystem
{
    public const int ChunkSize = PlanetMapComponent.ChunkSize;
    public const int ArraySize = ChunkSize * ChunkSize;

    public static Vector2i GetChunkOrigin(Vector2i tile)
    {
        // Floor-divide so negative tiles map correctly
        var x = (int) MathF.Floor((float) tile.X / ChunkSize);
        var y = (int) MathF.Floor((float) tile.Y / ChunkSize);
        return new Vector2i(x, y);
    }

    public static Vector2i GetRelativeTile(Vector2i tile, Vector2i chunkOrigin)
    {
        return tile - chunkOrigin * ChunkSize;
    }

    public static int GetTileIndex(Vector2i relativeTile)
    {
        return relativeTile.X * ChunkSize + relativeTile.Y;
    }
}

using Robust.Shared.Serialization;

namespace Content.Shared._Nibiru.PlanetMap;

/// <summary>
/// Sent client → server: player presses the "pen" (scan) button.
/// </summary>
[Serializable, NetSerializable]
public sealed class PlanetMapScanRequestMessage : EntityEventArgs
{
    public NetEntity MapEntity;

    public PlanetMapScanRequestMessage(NetEntity mapEntity)
    {
        MapEntity = mapEntity;
    }
}

/// <summary>
/// Sent server → client: signals that the map is being opened.
/// The client MUST clear its local chunk data upon receiving this.
/// Actual chunk data follows as one or more <see cref="PlanetMapChunkBatchMessage"/> packets.
/// </summary>
[Serializable, NetSerializable]
public sealed class PlanetMapOpenMessage : EntityEventArgs
{
    public NetEntity MapEntity;

    public PlanetMapOpenMessage(NetEntity mapEntity)
    {
        MapEntity = mapEntity;
    }
}

/// <summary>
/// Sent server → client: a streaming batch of chunk data.
/// Used for both initial map-open data (following PlanetMapOpenMessage)
/// and incremental scan results (following PlanetMapScanRequestMessage).
/// The client merges each batch into its local saved chunk store.
/// </summary>
[Serializable, NetSerializable]
public sealed class PlanetMapChunkBatchMessage : EntityEventArgs
{
    /// <summary>The map item entity this data belongs to.</summary>
    public NetEntity MapEntity;

    /// <summary>Chunk origin → flat tile-ID array (ChunkSize × ChunkSize).</summary>
    public Dictionary<Vector2i, uint[]> Chunks;

    /// <summary>Chunk origin → flat object-index array (ChunkSize × ChunkSize).</summary>
    public Dictionary<Vector2i, uint[]> Objects;

    /// <summary>Registry of entity prototype IDs referenced in <see cref="Objects"/>.</summary>
    public List<string> ObjectPrototypes;

    /// <summary>Chunk origin → flat zone-member array (ChunkSize × ChunkSize).</summary>
    public Dictionary<Vector2i, uint[]> Zones;

    /// <summary>Registry of <c>planetMapZone</c> prototype IDs referenced in <see cref="Zones"/>.</summary>
    public List<string> ZonePrototypes;

    /// <summary>True when this is the final batch in the current streaming sequence.</summary>
    public bool IsLast;

    /// <summary>
    /// For scan results: the exact tiles that were re-classified. The client must overwrite
    /// (including zeroing) ONLY these tiles; other tiles in the same chunk that weren't part of
    /// this scan are preserved. Null for initial map-open streams (everything overwrites).
    /// </summary>
    public HashSet<Vector2i>? OverwriteTiles;

    public PlanetMapChunkBatchMessage(
        NetEntity mapEntity,
        Dictionary<Vector2i, uint[]> chunks,
        Dictionary<Vector2i, uint[]> objects,
        List<string> objectPrototypes,
        Dictionary<Vector2i, uint[]> zones,
        List<string> zonePrototypes,
        bool isLast,
        HashSet<Vector2i>? overwriteTiles = null)
    {
        MapEntity        = mapEntity;
        Chunks           = chunks;
        Objects          = objects;
        ObjectPrototypes = objectPrototypes;
        Zones            = zones;
        ZonePrototypes   = zonePrototypes;
        IsLast           = isLast;
        OverwriteTiles   = overwriteTiles;
    }
}

using Robust.Shared.Serialization;

namespace Content.Shared._Nibiru.PlanetMap;

/// <summary>
/// Sent by client when the player presses the "pen" button on their map.
/// Server responds by scanning loaded chunks around the player and sending
/// data back.
/// </summary>
[Serializable, NetSerializable]
public sealed class PlanetMapScanRequestMessage : EntityEventArgs
{
    /// <summary>
    /// The map item entity that the player is using.
    /// </summary>
    public NetEntity MapEntity;

    public PlanetMapScanRequestMessage(NetEntity mapEntity)
    {
        MapEntity = mapEntity;
    }
}

/// <summary>
/// Sent server → client: a batch of newly scanned chunk data to merge into the
/// client-side persistent map.
/// </summary>
[Serializable, NetSerializable]
public sealed class PlanetMapChunkDataMessage : EntityEventArgs
{
    /// <summary>
    /// The map item entity that owns this data (used to route to the right BUI).
    /// </summary>
    public NetEntity MapEntity;

    /// <summary>
    /// Chunk origin → flat array of <see cref="PlanetMapTileType"/> (ChunkSize×ChunkSize).
    /// </summary>
    public Dictionary<Vector2i, uint[]> Chunks;
    public Dictionary<Vector2i, uint[]> Objects;
    public List<string> ObjectPrototypes;

    public PlanetMapChunkDataMessage(NetEntity mapEntity, Dictionary<Vector2i, uint[]> chunks, Dictionary<Vector2i, uint[]> objects, List<string> objectPrototypes)
    {
        MapEntity = mapEntity;
        Chunks    = chunks;
        Objects   = objects;
        ObjectPrototypes = objectPrototypes;
    }
}

/// <summary>
/// Sent server → client: full saved-map data when a player opens their map item.
/// </summary>
[Serializable, NetSerializable]
public sealed class PlanetMapOpenMessage : EntityEventArgs
{
    public NetEntity MapEntity;
    public Dictionary<Vector2i, uint[]> SavedChunks;
    public Dictionary<Vector2i, uint[]> SavedObjects;
    public List<string> ObjectPrototypes;

    public PlanetMapOpenMessage(NetEntity mapEntity, Dictionary<Vector2i, uint[]> savedChunks, Dictionary<Vector2i, uint[]> savedObjects, List<string> objectPrototypes)
    {
        MapEntity    = mapEntity;
        SavedChunks  = savedChunks;
        SavedObjects = savedObjects;
        ObjectPrototypes = objectPrototypes;
    }
}

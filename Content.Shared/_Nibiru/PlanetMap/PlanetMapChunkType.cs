namespace Content.Shared._Nibiru.PlanetMap;

/// <summary>
/// What kind of feature is recorded on the planet map for a given tile.
/// Each value is a visual category used for color-coding.
/// </summary>
public enum PlanetMapTileType : byte
{
    Empty   = 0,
    Ground  = 1,
    Water   = 2,  // rivers / lakes
    Sand    = 3,
    Snow    = 4,
    Rock    = 5,
    Lava    = 6,
    Wall    = 10, // structures / rocks blocking path
    Tree    = 11,
    Flower  = 12,
    Decal   = 13, // generic decal / object
    UnknownTile = 20, // Uses color hash
    UnknownObj  = 21, // Uses color hash
}

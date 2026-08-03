using System.Text.Json.Serialization;

namespace Content.Server._Nibiru.SaveLoad;

public sealed class RoundSaveManifest
{
    [JsonPropertyName("preset_id")]
    public string PresetId { get; set; } = string.Empty;

    [JsonPropertyName("saved_at")]
    public string SavedAt { get; set; } = string.Empty;

    [JsonPropertyName("maps")]
    public List<MapSaveData> Maps { get; set; } = new();

    [JsonPropertyName("players")]
    public List<PlayerSaveData> Players { get; set; } = new();
}

public sealed class PlayerSaveData
{
    [JsonPropertyName("user_id")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("character_name")]
    public string CharacterName { get; set; } = string.Empty;

    [JsonPropertyName("map_id")]
    public int MapId { get; set; }
}

public sealed class MapSaveData
{
    [JsonPropertyName("map_id")]
    public int MapId { get; set; }

    [JsonPropertyName("z_level")]
    public int ZLevel { get; set; }

    [JsonPropertyName("network_id")]
    public int NetworkId { get; set; }

    [JsonPropertyName("map_file")]
    public string MapFile { get; set; } = string.Empty;
}

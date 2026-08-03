namespace Content.Server._Nibiru.SaveLoad;

/// <summary>
/// Attached to player mobs saved on maps to pair them back to their UserId and character name on load.
/// </summary>
[RegisterComponent]
public sealed partial class NibiruSavedPlayerComponent : Component
{
    [DataField("userId")]
    public string UserId = string.Empty;

    [DataField("characterName")]
    public string CharacterName = string.Empty;
}

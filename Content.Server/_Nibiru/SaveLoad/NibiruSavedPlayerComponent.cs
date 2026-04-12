namespace Content.Server._Nibiru.SaveLoad;

[RegisterComponent]
public sealed partial class NibiruSavedPlayerComponent : Component
{
    [DataField("userId")]
    public string UserId = string.Empty;
}

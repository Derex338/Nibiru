using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Nibiru.Factions;

[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState(true)]
public sealed partial class FactionVisualsComponent : Component
{
    /// <summary>
    /// Name of faction whose logo and background to display.
    /// </summary>
    [DataField("factionName")]
    [AutoNetworkedField]
    public string FactionName = string.Empty;

    /// <summary>
    /// Saved background color of faction.
    /// </summary>
    [DataField("logoBackground")]
    [AutoNetworkedField]
    public Color LogoBackground = Color.Transparent;

    /// <summary>
    /// Saved pixels of faction logo.
    /// </summary>
    [DataField("logoPixels")]
    [AutoNetworkedField]
    public List<Color>? LogoPixels;


}

[Serializable, NetSerializable]
public enum FactionVisualLayers : byte
{
    Background,
    Logo
}

/// <summary>
/// Visual layers for faction statue.
/// </summary>
[Serializable, NetSerializable]
public enum FactionStatueVisualLayers : byte
{
    Statue
}

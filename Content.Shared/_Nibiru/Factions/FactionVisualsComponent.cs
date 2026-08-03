using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Nibiru.Factions;

[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState(true)]
public sealed partial class FactionVisualsComponent : Component
{
    /// <summary>
    /// Название фракции, чей логотип и фон нужно отобразить.
    /// </summary>
    [DataField("factionName")]
    [AutoNetworkedField]
    public string FactionName = string.Empty;

    /// <summary>
    /// Сохраненный цвет фона фракции.
    /// </summary>
    [DataField("logoBackground")]
    [AutoNetworkedField]
    public Color LogoBackground = Color.Transparent;

    /// <summary>
    /// Сохраненный рисунок фракции.
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
/// Визуальные слои для статуи фракции.
/// </summary>
[Serializable, NetSerializable]
public enum FactionStatueVisualLayers : byte
{
    Statue
}

using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Nibiru.Factions;

/// <summary>
/// A prototype for pre-defined faction logo sprites.
/// </summary>
[Prototype]
public sealed partial class FactionLogoPresetPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// Path to a specific 16x16 sprite file.
    /// When set, takes priority over SpriteFolder.
    /// </summary>
    [DataField]
    public ResPath? Sprite16 { get; private set; }

    /// <summary>
    /// Path to a specific 8x8 sprite file.
    /// When set, takes priority over SpriteFolder.
    /// </summary>
    [DataField]
    public ResPath? Sprite8 { get; private set; }

    /// <summary>
    /// Path to the folder containing the sprite files.
    /// The folder should contain [folderName].png (or [folderName]_16.png) for 16x16
    /// and [folderName]_8x8.png (or [folderName]_8.png) for 8x8.
    /// Used as a fallback when Sprite16/Sprite8 are not set.
    /// </summary>
    [DataField]
    public ResPath? SpriteFolder { get; private set; }
}

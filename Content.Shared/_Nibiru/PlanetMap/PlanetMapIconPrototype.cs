using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Robust.Shared.Maths;
using System.Collections.Generic;

namespace Content.Shared._Nibiru.PlanetMap;

/// <summary>
/// Prototype for custom icons on the planet map.
/// Replaces the default drawing shapes for specific entities.
/// </summary>
[Prototype]
public sealed partial class PlanetMapIconPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Entity prototype IDs that this icon applies to.
    /// </summary>
    [DataField("entities")]
    public List<string> Entities { get; private set; } = new();

    /// <summary>
    /// The shape to draw. If set to Sprite, it will draw the layers.
    /// </summary>
    [DataField("shape")]
    public PlanetMapIconShape Shape { get; private set; } = PlanetMapIconShape.Rectangle;

    /// <summary>
    /// Base color for the shape or symbol.
    /// </summary>
    [DataField("color")]
    public Color Color { get; private set; } = Color.White;

    /// <summary>
    /// Text to draw if shape is Symbol.
    /// </summary>
    [DataField("symbol")]
    public string? Symbol { get; private set; }

    /// <summary>
    /// If drawing sprites instead of primitive shapes.
    /// </summary>
    [DataField("layers")]
    public List<PlanetMapIconLayer> Layers { get; private set; } = new();

    /// <summary>
    /// Pattern to match entity IDs. If set, entities with IDs containing this string will match.
    /// This is used as a fallback if no exact match is found in the Entities list.
    /// </summary>
    [DataField("idPattern")]
    public string? IdPattern { get; private set; }

    [DataField("scale")]
    public float Scale { get; private set; } = 1.0f;
}

public enum PlanetMapIconShape : byte
{
    Rectangle,
    Circle,
    Sprite
}

[DataDefinition]
public sealed partial class PlanetMapIconLayer
{
    [DataField("sprite", required: true)]
    public SpriteSpecifier Sprite = default!;

    /// <summary>
    /// Fixed color for this layer.
    /// </summary>
    [DataField("color")]
    public Color Color = Color.White;

    /// <summary>
    /// Should this layer's color be tintable or modulated by default reading/code?
    /// </summary>
    [DataField("tintable")]
    public bool Tintable = false;
}

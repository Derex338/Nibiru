using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Robust.Shared.Maths;
using System.Collections.Generic;
using System.Numerics;

namespace Content.Shared._Nibiru.PlanetMap;

/// <summary>
/// Prototype describing a "zone" on the planet map: a region of many small grouped entities
/// (e.g. a thick forest) that renders as a smooth coloured/textured blob instead of
/// individual per-entity icons.
///
/// Classification is done on the server: an entity belonging to this zone is recorded in the
/// zone object layer only if it has at least <see cref="MinNeighbors"/> other zone members
/// within <see cref="Radius"/> tiles (i.e. it is part of a dense cluster).
/// The blob boundary is then produced client-side from this density, giving soft
/// diagonal-corners rather than hard square edges.
/// </summary>
[Prototype]
public sealed partial class PlanetMapZonePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Entity prototype IDs that belong to this zone.
    /// </summary>
    [DataField("entities")]
    public List<string> Entities { get; private set; } = new();

    /// <summary>
    /// Pattern-based fallback: entity IDs containing this string also belong to the zone.
    /// </summary>
    [DataField("idPattern")]
    public string? IdPattern { get; private set; }

    /// <summary>
    /// Euclidean distance (in tiles) that counts two zone members as "neighbours".
    /// Larger values merge sparse clusters into a single zone.
    /// </summary>
    [DataField("radius")]
    public float Radius { get; private set; } = 3f;

    /// <summary>
    /// Minimum total population of a cluster for it to be rendered as a zone blob. A cluster
    /// grows by flood-fill: every member within <see cref="Radius"/> of an already-accepted member
    /// joins, widening the search. Groups smaller than this stay as individual icons.
    /// </summary>
    [DataField("minNeighbors")]
    public int MinNeighbors { get; private set; } = 5;

    /// <summary>
    /// Solid colour used for the zone blob when <see cref="Background"/> is not provided.
    /// When both are set the sprite is drawn tinted with this colour.
    /// When neither is set a deterministic per-ID colour is used.
    /// </summary>
    [DataField("color")]
    public Color Color { get; private set; } = Color.White;

    /// <summary>
    /// Background texture tiled across the zone. The texture is sampled with repeat wrap, so a
    /// relatively small (e.g. 8×8 px) sprite is stretched nicely over the whole blob.
    /// </summary>
    [DataField("sprite")]
    public SpriteSpecifier? Sprite { get; private set; }

    /// <summary>
    /// How many map-tiles each repetition of <see cref="Sprite"/> spans. Lower = denser texture
    /// pattern, higher = more stretched. Only used when <see cref="Sprite"/> is set.
    /// </summary>
    [DataField("repeatScale")]
    public float TextureRepeatScale { get; private set; } = 4f;

    /// <summary>
    /// Staggering of the texture in units of a full repeat. E.g. (0.5, 0) shifts every other
    /// texture row by half its width, producing an offset-brickwork pattern. Values are in
    /// [0, 1) — they multiply the repeat size per texture row (Y).
    /// </summary>
    [DataField("textureOffset")]
    public Vector2 TextureOffset { get; private set; } = Vector2.Zero;

    /// <summary>
    /// When non-null, alpha of the fill (applies to both colour and texture).
    /// </summary>
    [DataField("alpha")]
    public float Alpha { get; private set; } = 1f;
}
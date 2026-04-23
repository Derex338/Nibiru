using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared._Nibiru.Construction;

/// <summary>
///     Component used to link entities across different Z-levels.
///     If one entity in the link is destroyed, all others should be as well.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MultiLevelConstructionComponent : Component
{
    /// <summary>
    ///     List of all entities in this multi-level structure.
    /// </summary>
    [DataField, AutoNetworkedField]
    public HashSet<EntityUid> LinkedEntities = new();

    /// <summary>
    ///     Is this entity the "origin" of the structure?
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool IsOrigin = false;

    /// <summary>
    ///     List of entities to spawn upon initialization.
    ///     Only processed if IsOrigin is true.
    /// </summary>
    [DataField("projections")]
    public List<MultiLevelProjectionData> Projections = new();

    /// <summary>
    ///     Should the projections be offset in the direction the entity is facing?
    /// </summary>
    [DataField("offsetByRotation")]
    public bool OffsetByRotation = false;
}

[DataDefinition]
public sealed partial class MultiLevelProjectionData
{
    [DataField("offset", required: true)]
    public int Offset;

    [DataField("prototype", required: true, customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string Prototype = string.Empty;

    /// <summary>
    ///     Local offset relative to the origin entity.
    /// </summary>
    [DataField("localOffset")]
    public Vector2 LocalOffset = Vector2.Zero;
}

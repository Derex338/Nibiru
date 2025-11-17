using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared._Nibiru.Construction;

/// <summary>
///     Вход и выход из пещеры.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CaveEnterComponent : Component
{
    /// <summary>
    ///     Sound played on arriving to this portal, centered on the destination.
    ///     The arrival sound of the entered portal will play if the destination is not a portal.
    /// </summary>
    [DataField("arrivalSound")]
    public SoundSpecifier ArrivalSound = new SoundPathSpecifier("/Audio/Effects/teleport_arrival.ogg");

    /// <summary>
    ///     Sound played on departing from this portal, centered on the original portal.
    /// </summary>
    [DataField("departureSound")]
    public SoundSpecifier DepartureSound = new SoundPathSpecifier("/Audio/Effects/teleport_departure.ogg");

    /// <summary>
    ///     If false, this portal will fail to teleport and fizzle out if attempting to send an entity to a different map
    /// </summary>
    /// <remarks>
    ///     Shouldn't be able to teleport people to centcomm or the eshuttle from the station
    /// </remarks>
    [ViewVariables(VVAccess.ReadWrite)]
    public bool CanTeleportToOtherMaps = true;

    [DataField("secondCaveEnterPrototype", customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string? SecondCaveEnterPrototype = null;

    [ViewVariables, DataField("firstCaveEnter")]
    public EntityUid? FirstCaveEnter = null;

    [ViewVariables, DataField("secondCaveEnter")]
    public EntityUid? SecondCaveEnter = null;
}


using Content.Shared.EntityTable.EntitySelectors;
using Content.Shared.Parallax.Biomes;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Shared._Nibiru.GameTicking.Rules;

/// <summary>
/// Взято за основу с RimFortress
/// </summary>
[RegisterComponent]
public sealed partial class NibiruSurvivalRuleComponent : Component
{
    /// <summary>
    /// Prototype of the entity the player will move into after entering a round
    /// </summary>
    //[DataField]
    //public EntProtoId PlayerProtoId = "RimFortressObserver";

    /// <summary>
    /// Biome template that will be used in the creation of the world
    /// </summary>
    [DataField(required: true)]
    public ProtoId<BiomeTemplatePrototype> Biome;

    /// <summary>
    /// Biome template that will be used in the creation of the cave
    /// </summary>
    [DataField(required: true)]
    public ProtoId<BiomeTemplatePrototype> CaveBiome;

    /// <summary>
    /// Duration of the day
    /// </summary>
    [DataField]
    public TimeSpan DayDuration = TimeSpan.FromMinutes(20);

    /// <summary>
    /// Components that will be added to the pops when spawned
    /// </summary>
    [DataField]
    public ComponentRegistry? PopsComponentsOverride = new();

    /// <summary>
    /// Table with random events that can happen on the world map
    /// </summary>
    [DataField]
    public EntityTableSelector? WorldEvents;

    /// <summary>
    /// Table with random global events that can happen on the world map
    /// </summary>
    [DataField]
    public EntityTableSelector? GlobalEvents;

    [ViewVariables]
    public TimeSpan LastEventTime;

    [ViewVariables]
    public int LastWaitPoints;

    [ViewVariables]
    public EntityUid WorldMap;

    [ViewVariables]
    public EntityUid CaveMap;
}

using Content.Shared.Construction.Prototypes;
using Content.Shared._Nibiru.EntityInspector;
using Robust.Shared.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Content.Shared.Humanoid;

namespace Content.Shared._Nibiru.Factions;

[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
//[InspectableComponent("Faction")] // Это было для теста
public sealed partial class FactionComponent : Component
{
    [AutoNetworkedField]
    [DataField("factionName")]
    //[InspectableField("Name")]
    public string FactionName { get; set; } = string.Empty;

    [AutoNetworkedField]
    [DataField("isCreator")]
    //[InspectableField("Creator", Detail = "The character is the founder of the faction and has special admin rights.")]
    public bool IsCreator { get; set; } = false;

    /// <summary>
    /// All of the recipe packs that the faction type has by default
    /// </summary>
    [DataField]
    public List<ProtoId<ConstructionPackPrototype>> StaticPacks = new() { "FactionBase" };

    [ViewVariables]
    public EntityUid? ResearchServer;

    [AutoNetworkedField]
    [ViewVariables]
    public List<EntityUid> Members { get; set; } = new();

    /// <summary>
    /// All members that ever been in faction
    /// </summary>
    [AutoNetworkedField]
    [ViewVariables]
    public List<FactionMemberRecord> AllMembers { get; set; } = new();

    /// <summary>
    /// Faction members data for UI
    /// </summary>
    [AutoNetworkedField]
    public List<FactionMemberData> MemberData { get; set; } = new();

    [AutoNetworkedField]
    [ViewVariables]
    public EntityUid Leader = default!;

    [AutoNetworkedField]
    [ViewVariables]
    public EntityUid Heir = default!;

    [AutoNetworkedField]
    [ViewVariables]
    public Color FactionColor = Color.Pink;

    /// <summary>
    /// Faction description
    /// </summary>
    [AutoNetworkedField]
    [DataField("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Faction icon (path to StatusIconPrototype)
    /// </summary>
    [AutoNetworkedField]
    [DataField("icon")]
    public string IconPath { get; set; } = string.Empty;

    /// <summary>
    /// Color of faction logo background
    /// </summary>
    [AutoNetworkedField]
    [DataField("logoBackground")]
    public Color LogoBackground { get; set; } = Color.Transparent;

    /// <summary>
    /// 32x32 logo drawing data
    /// </summary>
    [AutoNetworkedField]
    [DataField("logoPixels")]
    public List<Color> LogoPixels { get; set; } = new();

    /// <summary>
    /// 8x8 icon drawing data
    /// </summary>
    [AutoNetworkedField]
    [DataField("logoPixels8x8")]
    public List<Color> LogoPixels8x8 { get; set; } = new();

    /// <summary>
    /// Role of faction member
    /// </summary>
    [AutoNetworkedField]
    [DataField("rank")]
    public string Rank { get; set; } = string.Empty;

    /// <summary>
    /// Faction status
    /// </summary>
    [AutoNetworkedField]
    [DataField("status")]
    //[InspectableField("Status", Detail = "Current faction status: Active - active, Recruiting - open recruitment, AtWar - at war.")]
    public FactionStatus Status { get; set; } = FactionStatus.Active;

    /// <summary>
    /// Is faction recruiting
    /// </summary>
    [AutoNetworkedField]
    [DataField("recruiting")]
    //[InspectableField("Recruiting")]
    public bool IsRecruiting { get; set; } = false;

    /// <summary>
    /// Species filter (SpeciesPrototype)
    /// </summary>
    [AutoNetworkedField]
    [DataField("whiteListSpecies")]
    public List<string> WhiteListSpecies { get; set; } = new();

    /// <summary>
    /// Gender filter (Sex)
    /// </summary>
    [AutoNetworkedField]
    [DataField("whiteListGender")]
    public List<Sex> WhiteListGender { get; set; } = new();

    /// <summary>
    /// Skin color filter for different species
    /// </summary>
    [AutoNetworkedField]
    [DataField("whiteListSkinColors")]
    public Dictionary<string, FactionSkinColorFilter> WhiteListSkinColors { get; set; } = new();

    /// <summary>
    /// Name filter (comma separated)
    /// </summary>
    [AutoNetworkedField]
    [DataField("whiteListNames")]
    public List<string> WhiteListNames { get; set; } = new();

    /*string IInspectableComponent.InspectorDisplayName => "entity-inspector-comp-faction";

    IEnumerable<InspectableFieldData> IInspectableComponent.GetInspectableFields()
    {
        yield return new InspectableFieldData(
            "entity-inspector-faction-name", FactionName, Order: 0);
        yield return new InspectableFieldData(
            "entity-inspector-faction-creator", IsCreator,
            Detail: "entity-inspector-faction-creator-detail", Order: 1);
        yield return new InspectableFieldData(
            "entity-inspector-faction-status", Status,
            Detail: "entity-inspector-faction-status-detail", Order: 2);
        yield return new InspectableFieldData(
            "entity-inspector-faction-recruiting", IsRecruiting, Order: 3);
        yield return new InspectableFieldData(
            "entity-inspector-faction-members", Members, Order: 4);
    }*/

    [AutoNetworkedField]
    [DataField("roles")]
    public List<FactionRole> Roles { get; set; } = new();
}

[Serializable, NetSerializable, DataDefinition]
public partial struct FactionRole
{
    [DataField("name")]
    public string Name { get; set; }

    [DataField("canInvite")]
    public bool CanInvite { get; set; }

    [DataField("canResearch")]
    public bool CanResearch { get; set; }

    [DataField("canManageRoles")]
    public bool CanManageRoles { get; set; }

    [DataField("canInherit")]
    public bool CanInherit { get; set; }
}

/// <summary>
/// Skin color filter
/// </summary>
[Serializable, NetSerializable, DataDefinition]
public partial struct FactionSkinColorFilter
{
    [DataField("color")]
    public Color Color;

    [DataField("passHigher")]
    public bool PassHigher;
}

/// <summary>
/// Faction members data for UI
/// </summary>
[Serializable, NetSerializable, DataDefinition]
public partial struct FactionMemberData
{
    [DataField("entity")]
    public NetEntity Entity;

    [DataField("name")]
    public string Name;

    [DataField("rank")]
    public string Rank;
}

/// <summary>
/// Faction member record for list of all members who ever been in faction.
/// Stores EntityId of character for getting sprite.
/// </summary>
[Serializable, NetSerializable, DataDefinition]
public partial struct FactionMemberRecord
{
    [DataField("entity")]
    public NetEntity Entity;

    [DataField("name")]
    public string Name;

    [DataField("joinedTime")]
    public TimeSpan JoinedTime;
}

/// <summary>
/// Component for storing all factions on the map
/// Attached to the map entity
/// </summary>
[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class FactionRegistryComponent : Component
{
    /// <summary>
    /// All factions registered on the map
    /// Key - faction name, value - faction data
    /// </summary>
    [AutoNetworkedField]
    [DataField("factions")]
    public Dictionary<string, FactionRegistryData> Factions { get; set; } = new();
}

/// <summary>
/// Faction data
/// </summary>
[Serializable, NetSerializable]
[DataDefinition]
public partial struct FactionRegistryData
{
    /// <summary>
    /// Faction name
    /// </summary>
    [DataField("name")]
    public string Name;

    /// <summary>
    /// Faction leader (serialized)
    /// </summary>
    [DataField("leader")]
    public NetEntity Leader;

    /// <summary>
    /// List of all faction members (serialized)
    /// </summary>
    [DataField("members")]
    public List<NetEntity> Members;

    /// <summary>
    /// List of all members who ever been in faction (serialized)
    /// </summary>
    [DataField("allMembers")]
    public List<FactionMemberRecord> AllMembers;

    /// <summary>
    /// Faction color
    /// </summary>
    [DataField("color")]
    public Color Color;

    /// <summary>
    /// Faction description
    /// </summary>
    [DataField("description")]
    public string Description;

    /// <summary>
    /// Faction icon path
    /// </summary>
    [DataField("icon")]
    public string IconPath;

    /// <summary>
    /// Faction logo background color
    /// </summary>
    [DataField("logoBackground")]
    public Color LogoBackground;

    /// <summary>
    /// 32x32 logo drawing data
    /// </summary>
    [DataField("logoPixels")]
    public List<Color> LogoPixels;

    /// <summary>
    /// 8x8 logo drawing data
    /// </summary>
    [DataField("logoPixels8x8")]
    public List<Color> LogoPixels8x8;

    /// <summary>
    /// Faction status
    /// </summary>
    [DataField("status")]
    public FactionStatus Status;

    /// <summary>
    /// Is recruiting
    /// </summary>
    [DataField("recruiting")]
    public bool IsRecruiting;

    /// <summary>
    /// Faction creation time
    /// </summary>
    [DataField("created")]
    public TimeSpan Created;

    /// <summary>
    /// Admission filters
    /// </summary>
    [DataField("whiteListSpecies")]
    public List<string> WhiteListSpecies;

    [DataField("whiteListGender")]
    public List<Sex> WhiteListGender;

    [DataField("whiteListSkinColors")]
    public Dictionary<string, FactionSkinColorFilter> WhiteListSkinColors;

    [DataField("whiteListNames")]
    public List<string> WhiteListNames;

    /// <summary>
    /// Faction roles list
    /// </summary>
    [DataField("roles")]
    public List<FactionRole> Roles;
}

/// <summary>
/// Faction status
/// </summary>
[Serializable, NetSerializable]
public enum FactionStatus : byte
{
    Active,      // Active
    Recruiting,  // Recruiting members
    AtWar        // At war
}

/// <summary>
/// Faction information for UI
/// </summary>
[Serializable, NetSerializable]
public sealed class FactionInfo
{
    public string FactionName { get; set; } = string.Empty;
    public int MemberCount { get; set; }
    public Color Color { get; set; } = Color.White;
    public string Description { get; set; } = string.Empty;
    public string IconPath { get; set; } = string.Empty;
    public Color LogoBackground { get; set; } = Color.Transparent;
    public List<Color> LogoPixels { get; set; } = new();
    public List<Color> LogoPixels8x8 { get; set; } = new();
    public FactionStatus Status { get; set; } = FactionStatus.Active;
    public bool IsRecruiting { get; set; } = false;
    public List<string> WhiteListSpecies { get; set; } = new();
    public List<Sex> WhiteListGender { get; set; } = new();
    public Dictionary<string, FactionSkinColorFilter> WhiteListSkinColors { get; set; } = new();
    public List<string> WhiteListNames { get; set; } = new();
    public NetEntity Leader { get; set; }
    public List<FactionRole> Roles { get; set; } = new();
}

/// <summary>
/// Message for requesting list of factions
/// </summary>
[Serializable, NetSerializable]
public sealed class RequestFactionsMessage : EntityEventArgs
{
}

/// <summary>
/// Message with list of available factions
/// </summary>
[Serializable, NetSerializable]
public sealed class AvailableFactionsMessage : EntityEventArgs
{
    public List<FactionInfo> Factions { get; set; } = new();
}

/// <summary>
/// Message for joining faction through late join
/// </summary>
[Serializable, NetSerializable]
public sealed class LateJoinFactionMessage : EntityEventArgs
{
    public string? FactionName { get; set; }
}

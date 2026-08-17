using Content.Shared.Eui;
using Robust.Shared.GameStates;
using Robust.Shared.Maths;
using Robust.Shared.Serialization;
using System.Numerics;

namespace Content.Shared._Nibiru.Factions;

/// <summary>
/// Captured sprite layer of character for eternal display on statue.
/// </summary>
[Serializable, NetSerializable, DataDefinition]
public partial struct CapturedSpriteLayer
{
    [DataField]
    public string RsiPath;

    [DataField]
    public string State;

    [DataField]
    public Color? Color;

    [DataField]
    public Vector2? Offset;

    [DataField]
    public bool Visible = true;
}

/// <summary>
/// Component of faction member statue.
/// Stores captured sprite of selected member + list of all members for selection.
/// </summary>
[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class FactionStatueComponent : Component
{
    /// <summary>
    /// Name of faction this statue belongs to.
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public string FactionName = string.Empty;

    /// <summary>
    /// All members of faction available for selection (at time of construction).
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public List<FactionMemberRecord> AllMembers { get; set; } = new();

    /// <summary>
    /// Selected faction member.
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public NetEntity? SelectedMember { get; set; }

    /// <summary>
    /// Name of selected member.
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public string SelectedMemberName = string.Empty;

    [DataField]
    [AutoNetworkedField]
    public List<CapturedSpriteLayer> CapturedLayers { get; set; } = new();

    /// <summary>
    /// Builder entity (server-side).
    /// </summary>
    [DataField]
    public EntityUid? Builder;
}

/// <summary>
/// EUI state for choosing faction member at time of statue construction.
/// </summary>
[Serializable, NetSerializable]
public sealed class FactionStatueSelectionState : EuiStateBase
{
    public NetEntity StatueEntity;
    public string FactionName = string.Empty;
    public List<FactionMemberRecord> AllMembers = new();
}

/// <summary>
/// Message from client with selected faction member for statue.
/// </summary>
[Serializable, NetSerializable]
public sealed class FactionStatueSelectMessage : EuiMessageBase
{
    public NetEntity SelectedMember;
}

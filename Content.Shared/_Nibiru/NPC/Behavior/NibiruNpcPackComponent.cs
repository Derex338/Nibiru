using Robust.Shared.GameStates;

namespace Content.Shared._Nibiru.NPC.Behavior;

/// <summary>
/// Pack component: combines NPCs into groups with common targeting.
/// When one pack member detects an enemy, the entire pack receives information about the target.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NibiruNpcPackComponent : Component
{
    /// <summary>
    /// Pack ID. NPCs with the same PackId act together.
    /// Generated when a group is spawned or joins it.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public string? PackId;

    /// <summary>
    /// Maximum communication range within the pack.
    /// If a pack member is farther away, it does not receive updates from its kin.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float PackCommunicationRange = 15f;

    /// <summary>
    /// Is this NPC the leader (alpha) of the pack?
    /// Killing the leader causes panic or disorganization in the pack.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public bool IsLeader;

    /// <summary>
    /// EntityUid of the pack leader followed by members.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public EntityUid? LeaderUid;

    /// <summary>
    /// Distance to follow the leader.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float FollowLeaderRange = 4f;

    /// <summary>
    /// Duration of the pack panic state after the leader is killed (in seconds).
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float PanicDuration = 15f;

    /// <summary>
    /// Remaining panic time (if the leader is killed).
    /// </summary>
    [ViewVariables]
    public float PanicTimer;
}

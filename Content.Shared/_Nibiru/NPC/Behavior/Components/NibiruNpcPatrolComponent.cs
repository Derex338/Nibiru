using Robust.Shared.GameStates;
using Robust.Shared.Map;

namespace Content.Shared._Nibiru.NPC.Behavior.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NibiruNpcPatrolComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public float PatrolRadius = 8f;

    [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public float PatrolInterval = 5f;

    [ViewVariables, AutoNetworkedField]
    public float PatrolAccumulator;
}

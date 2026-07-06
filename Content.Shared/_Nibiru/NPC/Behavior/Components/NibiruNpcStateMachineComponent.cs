using Robust.Shared.GameStates;
using Robust.Shared.Map;

namespace Content.Shared._Nibiru.NPC.Behavior.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NibiruNpcStateMachineComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public NibiruNpcState CurrentState = NibiruNpcState.Idle;

    [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public NibiruNpcBehaviorType BehaviorType = NibiruNpcBehaviorType.Neutral;

    [ViewVariables, AutoNetworkedField]
    public Content.Shared._Nibiru.NPC.Training.NibiruAnimalCommand? CurrentCommand;

    [ViewVariables]
    public EntityUid? CurrentTarget;

    [ViewVariables]
    public EntityCoordinates? HomePosition;
}

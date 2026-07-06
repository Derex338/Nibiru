using Robust.Shared.GameStates;

namespace Content.Shared._Nibiru.NPC.Behavior.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NibiruNpcAggroComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public float AggroRange = 8f;

    [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public float DeaggroRange = 15f;

    [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public float FleeRange = 6f;

    [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public float FleeDistance = 12f;
}

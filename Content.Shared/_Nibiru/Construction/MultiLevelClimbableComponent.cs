using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Nibiru.Construction;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MultiLevelClimbableComponent : Component
{
    [DataField, AutoNetworkedField]
    public float ClimbDuration = 1.5f;
}

[Serializable, NetSerializable]
public sealed partial class MultiLevelClimbDoAfterEvent : SimpleDoAfterEvent
{
}

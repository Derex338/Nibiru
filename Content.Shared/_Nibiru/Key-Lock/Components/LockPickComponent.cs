using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Content.Shared.DoAfter;
using Content.Shared.Eui;

namespace Content.Shared._Nibiru.Lock;

[RegisterComponent, NetworkedComponent]
public partial class LockPickComponent : Component
{	

}

[Serializable, NetSerializable]
public sealed partial class LockPickDoAfter : DoAfterEvent
{
    public override DoAfterEvent Clone()
    {
        return this;
    }
}

[DataDefinition]
public sealed partial class LockPickCompleateEvent : EntityEventArgs
{

}

[Serializable, NetSerializable]
public sealed class KeyCodeState() : EuiStateBase
{
    
}

[Serializable, NetSerializable]
public sealed class KeyCodeMessage(int Code) : EuiMessageBase
{
    public readonly int Code = Code;
}

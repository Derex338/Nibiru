using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Content.Shared.DoAfter;

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
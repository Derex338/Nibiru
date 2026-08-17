using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared._Nibiru.Key;

[Serializable, NetSerializable]
public sealed class KeyCodeState() : EuiStateBase
{
    public bool Close = false;
}

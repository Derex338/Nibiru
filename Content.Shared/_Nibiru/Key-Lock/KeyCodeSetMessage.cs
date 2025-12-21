using Content.Shared.DoAfter;
using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared._Nibiru.Key;

[Serializable, NetSerializable]
public sealed class KeyCodeSetMessage(bool isCodeSet, int code) : EuiMessageBase
{
    public readonly bool IsCodeSet = isCodeSet;
    public readonly int Code = code;
}

[Serializable, NetSerializable]
public sealed partial class KeyCodeSetEvent : SimpleDoAfterEvent
{
    public int Code;

    public KeyCodeSetEvent(int code)
    {
        Code = code;
    }
}

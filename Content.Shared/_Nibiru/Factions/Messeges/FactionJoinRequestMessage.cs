using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared._Nibiru.Factions.Messages;

[Serializable, NetSerializable]
public sealed class FactionJoinRequestMessage(bool isAccepted) : EuiMessageBase
{
    public readonly bool IsAccepted = isAccepted;
}
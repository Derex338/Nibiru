using System;
using Robust.Shared.Serialization;

namespace Content.Shared._Nibiru.Factions;

[Serializable, NetSerializable]
public sealed class FactionCreateRequestMessage : EntityEventArgs
{
    public string FactionName { get; set; } = string.Empty;
}

[Serializable, NetSerializable]
public sealed class HeirChooseMessage : EntityEventArgs
{
    public NetEntity Heir = default!;
}

[Serializable, NetSerializable]
public sealed class FactionTitleTransferMessage : EntityEventArgs
{
    public NetEntity entity = default!;
}

[Serializable, NetSerializable]
public sealed class FactionLeaveMessage : EntityEventArgs
{
}

[Serializable, NetSerializable]
public sealed class FactionDeleteMessage : EntityEventArgs
{
}

[Serializable, NetSerializable]
public sealed class FactionChangeStateMessage : EntityEventArgs
{
    public Color? Color = null;
    public string? FactionName = null;
}

[Serializable, NetSerializable]
public sealed class FactionKickMemberMessage : EntityEventArgs
{
    public NetEntity Member = default;
}

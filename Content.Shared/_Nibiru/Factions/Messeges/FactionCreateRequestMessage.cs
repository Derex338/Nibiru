using System;
using Robust.Shared.Serialization;

namespace Content.Shared._Nibiru.Factions
{
    [Serializable, NetSerializable]
    public sealed class FactionCreateRequestMessage : EntityEventArgs
    {
        public string FactionName { get; set; } = string.Empty;
    }
}
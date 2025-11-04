using System;
using Robust.Shared.Serialization;
using Robust.Shared.GameObjects;
using Content.Shared._Nibiru.Factions;

namespace Content.Shared._Nibiru.Factions
{
    [Serializable, NetSerializable]
    public sealed class FactionStateRequestMessage : EntityEventArgs
    {
        public bool creator = false;
    }
}
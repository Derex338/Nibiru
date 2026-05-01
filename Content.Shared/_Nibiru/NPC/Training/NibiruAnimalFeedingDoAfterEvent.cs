using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Nibiru.NPC.Training;

[Serializable, NetSerializable]
public sealed partial class NibiruAnimalFeedingDoAfterEvent : DoAfterEvent
{
    public override DoAfterEvent Clone() => this;
}

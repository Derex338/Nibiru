using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Nibiru.NPC.Livestock;

[Serializable, NetSerializable]
public sealed partial class NibiruAnimalMatingDoAfterEvent : DoAfterEvent
{
    public override DoAfterEvent Clone() => this;
}

using Robust.Shared.Serialization;
using Robust.Shared.Maths;

namespace Content.Shared._Nibiru.Factions.Messeges;

[Serializable, NetSerializable]
public sealed class NibiruFactionLogoSaveMessage : EntityEventArgs
{
    public Color BackgroundColor { get; set; }
    public List<Color> Pixels { get; set; } = new();
    public List<Color> Pixels8x8 { get; set; } = new();
}

using System;
using Robust.Shared.Serialization;
using Robust.Shared.Maths;

namespace Content.Shared._Nibiru.Factions;

[Serializable, NetSerializable]
public sealed class NibiruFactionLeaderPrefsMessage : EntityEventArgs
{
    public string FactionName = "Новая Фракция";
    public string Description = "";
    public Color Color = Color.White;
    public string IconPath = "/Textures/Interface/Misc/job_icons.rsi/Cargo/cargo_technician.png";
    public bool IsRecruiting = true;
}

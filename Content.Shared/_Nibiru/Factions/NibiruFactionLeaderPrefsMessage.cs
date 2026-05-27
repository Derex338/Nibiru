using System;
using System.Collections.Generic;
using Robust.Shared.Serialization;
using Robust.Shared.Maths;
using Content.Shared.Humanoid;

namespace Content.Shared._Nibiru.Factions;

[Serializable, NetSerializable]
public sealed class NibiruFactionLeaderPrefsMessage : EntityEventArgs
{
    public string FactionName = "Новая Фракция";
    public string Description = "";
    public Color Color = Color.White;
    public string IconPath = "/Textures/Interface/Misc/job_icons.rsi/Cargo/cargo_technician.png";
    public bool IsRecruiting = true;

    public List<Color> Logo16 = new();
    public List<Color> Logo8 = new();
    public Color LogoBackground = Color.Transparent;

    public List<string> FilterSpecies = new();
    public string FilterGender = "All";
    public string FilterName = "";

    public List<FactionRole> Roles = new();
}

using Robust.Shared.Serialization;
using Content.Server.Botany;

namespace Content.Server._Nibiru.Research.Components;

[Serializable, NetSerializable]
public sealed class HarvestPlantMessage : EntityEventArgs
{
    public EntityUid _user;
    public SeedData _seed;

    public HarvestPlantMessage(EntityUid user, SeedData seed)
    {
        _user = user;
        _seed = seed;
    }
}

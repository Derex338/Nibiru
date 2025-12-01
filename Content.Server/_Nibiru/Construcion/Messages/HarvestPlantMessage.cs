using Robust.Shared.Serialization;
using Content.Server.Botany;

namespace Content.Server._Nibiru.Research.Components;

[Serializable]
public sealed class HarvestPlantMessage : EntityEventArgs
{
    public SeedData _seed;

    public HarvestPlantMessage(SeedData seed)
    {
        _seed = seed;
    }
}

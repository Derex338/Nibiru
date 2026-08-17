using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Nibiru.NPC.Livestock;

/// <summary>
/// DoAfter completion event for harvesting resources from an animal (shearing, milking).
/// </summary>
[Serializable, NetSerializable]
public sealed partial class LivestockHarvestDoAfterEvent : SimpleDoAfterEvent
{
    /// <summary>
    /// Index of the resource in the HarvestableResources list.
    /// </summary>
    [DataField]
    public int ResourceIndex;

    public LivestockHarvestDoAfterEvent(int resourceIndex)
    {
        ResourceIndex = resourceIndex;
    }

    public LivestockHarvestDoAfterEvent() { }
}

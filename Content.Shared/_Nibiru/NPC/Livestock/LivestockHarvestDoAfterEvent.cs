using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Nibiru.NPC.Livestock;

/// <summary>
/// Событие завершения DoAfter при сборе ресурсов с животного (стрижка, дойка).
/// </summary>
[Serializable, NetSerializable]
public sealed partial class LivestockHarvestDoAfterEvent : SimpleDoAfterEvent
{
    /// <summary>
    /// Индекс ресурса в списке HarvestableResources.
    /// </summary>
    [DataField]
    public int ResourceIndex;

    public LivestockHarvestDoAfterEvent(int resourceIndex)
    {
        ResourceIndex = resourceIndex;
    }

    public LivestockHarvestDoAfterEvent() { }
}

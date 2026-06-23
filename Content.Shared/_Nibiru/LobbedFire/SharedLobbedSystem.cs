using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;

namespace Content.Shared._Nibiru.LobbedFire;

public abstract partial class SharedLobbedSystem : EntitySystem
{
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Track indicator lifetime (client handles visual scaling)
        var indQuery = EntityQueryEnumerator<LobbedIndicatorComponent>();
        while (indQuery.MoveNext(out var uid, out var indicator))
        {
            indicator.TimeAlive += frameTime;
            if (indicator.TimeAlive >= indicator.FlightDuration)
                QueueDel(uid);
        }
    }
}

[Serializable, NetSerializable]
public enum NibiruLobbedArrowVisuals : byte
{
    Grounded,
}

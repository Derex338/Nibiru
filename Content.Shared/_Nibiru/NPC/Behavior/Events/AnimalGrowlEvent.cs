namespace Content.Shared._Nibiru.NPC.Behavior.Events;

public sealed class AnimalGrowlEvent : EntityEventArgs
{
    public EntityUid IntruderUid;

    public AnimalGrowlEvent(EntityUid intruder)
    {
        IntruderUid = intruder;
    }
}

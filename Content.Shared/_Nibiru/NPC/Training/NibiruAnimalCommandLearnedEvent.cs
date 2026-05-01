namespace Content.Shared._Nibiru.NPC.Training;

/// <summary>
/// Raised when an animal learns a new command.
/// </summary>
public sealed class NibiruAnimalCommandLearnedEvent : EntityEventArgs
{
    public readonly EntityUid Animal;
    public readonly NibiruAnimalCommand Command;

    public NibiruAnimalCommandLearnedEvent(EntityUid animal, NibiruAnimalCommand command)
    {
        Animal = animal;
        Command = command;
    }
}

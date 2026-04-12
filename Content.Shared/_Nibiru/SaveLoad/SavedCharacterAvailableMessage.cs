using System.Collections.Generic;
using Robust.Shared.Serialization;
using System.Collections.Generic;

namespace Content.Shared._Nibiru.SaveLoad;

[Serializable, NetSerializable]
public sealed class RequestSavedCharacterMessage : EntityEventArgs
{
}

[Serializable, NetSerializable]
public sealed class SavedCharacterAvailableMessage : EntityEventArgs
{
    public List<string> CharacterNames = new();

    public SavedCharacterAvailableMessage() { }

    public SavedCharacterAvailableMessage(List<string> characterNames)
    {
        CharacterNames = characterNames;
    }
}

using Robust.Shared.Serialization;

namespace Content.Shared._Nibiru.NPC.Training;

[Serializable, NetSerializable]
public enum NibiruAnimalTrainingUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class NibiruAnimalTrainingBuiState : BoundUserInterfaceState
{
    public float TrustLevel;
    public float MaxTrust;
    public bool IsTamed;
    
    public bool HasMountFear;
    public float StressTraining;
    public float MaxStressTraining;

    public bool Trainable;
    public HashSet<NibiruAnimalCommand> PossibleCommands;
    public HashSet<NibiruAnimalCommand> LearnedCommands;

    public NibiruAnimalTrainingBuiState(float trustLevel, float maxTrust, bool isTamed, 
        bool hasMountFear, float stressTraining, float maxStressTraining, 
        bool trainable, HashSet<NibiruAnimalCommand> possibleCommands, HashSet<NibiruAnimalCommand> learnedCommands)
    {
        TrustLevel = trustLevel;
        MaxTrust = maxTrust;
        IsTamed = isTamed;
        HasMountFear = hasMountFear;
        StressTraining = stressTraining;
        MaxStressTraining = maxStressTraining;
        Trainable = trainable;
        PossibleCommands = possibleCommands;
        LearnedCommands = learnedCommands;
    }
}

[Serializable, NetSerializable]
public sealed class NibiruAnimalTrainCommandMessage : BoundUserInterfaceMessage
{
    public NibiruAnimalCommand Command;

    public NibiruAnimalTrainCommandMessage(NibiruAnimalCommand command)
    {
        Command = command;
    }
}

[Serializable, NetSerializable]
public sealed class NibiruAnimalTrainStressMessage : BoundUserInterfaceMessage
{
}

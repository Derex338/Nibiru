using Content.Shared.Backmen.Research;
using Robust.Shared.Serialization;

namespace Content.Shared.Research.Components
{
    [NetSerializable, Serializable]
    public enum ResearchConsoleUiKey : byte
    {
        Key,
    }

    [Serializable, NetSerializable]
    public sealed class ConsoleUnlockTechnologyMessage : BoundUserInterfaceMessage
    {
        public string Id;

        public ConsoleUnlockTechnologyMessage(string id)
        {
            Id = id;
        }
    }

    [Serializable, NetSerializable]
    public sealed class ConsoleServerSelectionMessage : BoundUserInterfaceMessage
    {

    }

    [Serializable, NetSerializable]
    public sealed class ResearchConsoleBoundInterfaceState : BoundUserInterfaceState
    {
        public int Points;

        /// <summary>
        /// Goobstation field - all researches and their availablities
        /// </summary>
        public Dictionary<string, ResearchAvailability> Researches;

        public string CurrentEpoch;//Also Nibiru
        public List<string> UnlockedEpochs; //Nibiru

        public ResearchConsoleBoundInterfaceState(
            int points,
            Dictionary<string, ResearchAvailability> researches,
            string currentEpoch,
            List<string> unlockedEpochs)
        {
            Points = points;
            Researches = researches;
            CurrentEpoch = currentEpoch;
            UnlockedEpochs = unlockedEpochs;
        }
    }

    [Serializable, NetSerializable]
    public sealed class ConsoleChangeEpochMessage : BoundUserInterfaceMessage //Nibiru
    {
        public string EpochId;

        public ConsoleChangeEpochMessage(string epochId)
        {
            EpochId = epochId;
        }
    }
}

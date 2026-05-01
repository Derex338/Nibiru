using Content.Shared._Nibiru.NPC;
using Content.Shared._Nibiru.NPC.Training;
using Robust.Client.GameObjects;

namespace Content.Client._Nibiru.NPC.UI;

public sealed class NibiruAnimalTrainingBoundUserInterface : BoundUserInterface
{
    private NibiruAnimalTrainingWindow? _window;

    public NibiruAnimalTrainingBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = new NibiruAnimalTrainingWindow();
        
        _window.OnClose += Close;
        _window.OnTrainCommand += command => SendMessage(new NibiruAnimalTrainCommandMessage(command));
        _window.OnTrainStress += () => SendMessage(new NibiruAnimalTrainStressMessage());

        _window.OpenCentered();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is NibiruAnimalTrainingBuiState trainingState)
        {
            _window?.UpdateState(trainingState);
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _window?.Dispose();
        }
    }
}

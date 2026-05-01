using Content.Shared._Nibiru.NPC.Utility;
using Robust.Client.GameObjects;

namespace Content.Client._Nibiru.NPC.UI;

public sealed class NibiruBirdDeliveryBoundUserInterface : BoundUserInterface
{
    private NibiruBirdDeliveryWindow? _window;

    public NibiruBirdDeliveryBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = new NibiruBirdDeliveryWindow();
        _window.OnClose += Close;
        _window.OnPostSelected += (uid) =>
        {
            SendMessage(new NibiruBirdSelectPostMessage(uid));
            Close();
        };

        _window.OpenCentered();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not NibiruBirdDeliveryUiState s) return;

        _window?.UpdateState(s);
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

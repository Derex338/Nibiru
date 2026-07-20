using Content.Shared._Nibiru.NPC.Utility;
using Robust.Client.GameObjects;

namespace Content.Client._Nibiru.NPC.UI;

public sealed class NibiruBirdPostRenameBoundUserInterface : BoundUserInterface
{
    private NibiruBirdPostRenameWindow? _window;

    public NibiruBirdPostRenameBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = new NibiruBirdPostRenameWindow();
        _window.OnClose += Close;
        _window.OnRename += (name) =>
        {
            SendMessage(new NibiruRenamePostMessage(name));
            Close();
        };

        _window.OpenCentered();
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

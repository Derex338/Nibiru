using Content.Shared._Nibiru.PlanetMap;
using Robust.Client.UserInterface;

namespace Content.Client._Nibiru.PlanetMap.UI;

public sealed class PlanetMapBoundUserInterface : BoundUserInterface
{


    public PlanetMapBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        //_window = new PlanetMapWindow();
        //_window.OnClose += Close;
        //_window.OpenCentered();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing && false) //_window != null
        {
            //_window?.Dispose();
        }
    }
}

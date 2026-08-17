using System.Linq;
using System.Numerics;
using Content.Client.Eui;
using Content.Shared._Nibiru.Factions;
using Content.Shared._Nibiru.Factions.Messages;
using Content.Shared._Nibiru.Key;
using Content.Shared.Eui;
using JetBrains.Annotations;
using Robust.Client.Graphics;

namespace Content.Client._Nibiru.Key.UI;

[UsedImplicitly]
public sealed class KeyCodeSetEui : BaseEui
{
    private readonly KeyCodeSet _window;

    public KeyCodeSetEui()
    {
        _window = new KeyCodeSet();

        _window.OnCancelled += () => SendMessage(new KeyCodeSetMessage(false, 00000));

        _window.OnCodeSubmitted += (code) =>
        {
            SendMessage(new KeyCodeSetMessage(true, code));
            _window.Close();
        };
    }

    public override void Opened()
    {
        IoCManager.Resolve<IClyde>().RequestWindowAttention();

        // Open window somewhere below center of screen!
        // We don't want to hide what is going around the player
        _window.OpenCenteredAt(new Vector2(0.5f, 0.75f));
    }

    public override void Closed()
    {
        _window.Close();
    }
}

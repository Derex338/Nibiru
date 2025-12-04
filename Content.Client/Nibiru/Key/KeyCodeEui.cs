using Content.Client.Eui;
using Content.Client.Nibiru.Faction.UI;
using Content.Client.Nibiru.Key.UI;
using Content.Shared._Nibiru.Lock;
using Content.Shared.Eui;
using JetBrains.Annotations;
using Robust.Client.Graphics;
using Robust.Shared.Utility;
using System.Numerics;

namespace Content.Client.Nibiru.Key;

[UsedImplicitly]
public sealed class KeyCodeEui : BaseEui
{
    private readonly KeyCodeSet _window;

    public KeyCodeEui()
    {
        _window = new KeyCodeSet();

        _window.OnClose += () =>
        {
            if (_window.CodeLabel.Text == "")
                SendMessage(new KeyCodeMessage(00000));
            else
                SendMessage(new KeyCodeMessage(Parse.Int32(_window.CodeLabel.Text)));
        };

        _window.OnSetCode += code =>
        {
            SendMessage(new KeyCodeMessage(Parse.Int32(code)));
            _window.Close();
        };
    }
    public override void HandleState(EuiStateBase state)
    {
        //if (state is FactionJoinRequestMessage consentState)
        //{
        //    _window.SetConverterName(consentState.ConverterName);
        //}
    }

    public override void Opened()
    {
        IoCManager.Resolve<IClyde>().RequestWindowAttention();

        // Open window somewhere below center of screen!
        // We don't want to hide what is going around the player
        _window.OpenCentered();
    }

    public override void Closed()
    {
        _window.Close();
    }

}

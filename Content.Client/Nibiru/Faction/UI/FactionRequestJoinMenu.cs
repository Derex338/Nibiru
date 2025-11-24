using System.Numerics;
using Content.Client.Eui;
using Content.Shared._Nibiru.Factions;
using Content.Shared._Nibiru.Factions.Messages;
using Content.Shared.Eui;
using JetBrains.Annotations;
using Robust.Client.Graphics;

namespace Content.Client.Nibiru.Faction.UI;

[UsedImplicitly]
public sealed class FactionRequestedEui : BaseEui
{
    private readonly FactionRequestJoin _window;

    public FactionRequestedEui()
    {
        _window = new FactionRequestJoin();

        _window.OnDeny += () =>
        {
            SendMessage(new FactionJoinRequestMessage(false));
            _window.Close();
        };

        _window.OnClose += () => SendMessage(new FactionJoinRequestMessage(false));

        _window.OnAccept += () =>
        {
            SendMessage(new FactionJoinRequestMessage(true));
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
        _window.OpenCenteredAt(new Vector2(0.5f, 0.75f));
    }

    public override void Closed()
    {
        _window.Close();
    }
}

using Content.Client.Localization;
using Content.Client.UserInterface.Screens;
using Content.Client.UserInterface.Systems.Gameplay;
using Content.Client.Voting;
using JetBrains.Annotations;
using Robust.Client.UserInterface.Controllers;

namespace Content.Client.UserInterface.Systems.Vote;

[UsedImplicitly]
public sealed partial class VoteUIController : UIController, ILanguageRefreshable
{
    [Dependency] private IVoteManager _votes = default!;

    public override void Initialize()
    {
        base.Initialize();
        LanguageRefreshManager.Register(this);
        var gameplayStateLoad = UIManager.GetUIController<GameplayStateLoadController>();
        gameplayStateLoad.OnScreenLoad += OnScreenLoad;
        gameplayStateLoad.OnScreenUnload += OnScreenUnload;
    }

    private void OnScreenLoad()
    {
        switch (UIManager.ActiveScreen)
        {
            case DefaultGameScreen game:
                _votes.SetPopupContainer(game.VoteMenu);
                break;
            case SeparatedChatGameScreen separated:
                _votes.SetPopupContainer(separated.VoteMenu);
                break;
        }
    }

    private void OnScreenUnload()
    {
        _votes.ClearPopupContainer();
    }

    public void OnLanguageChanged()
    {
        // No-op: vote content comes from server, no localized UI to refresh.
    }
}

using Robust.Shared.IoC;

namespace Content.Client.UserInterface.Systems.Chat.Widgets;

public sealed partial class ChatBoxRu : ChatBox
{
    public ChatBoxRu()
    {
        IoCManager.InjectDependencies(this);
    }
}

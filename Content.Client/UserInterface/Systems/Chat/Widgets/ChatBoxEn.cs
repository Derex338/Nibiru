using Robust.Shared.IoC;

namespace Content.Client.UserInterface.Systems.Chat.Widgets;

public sealed partial class ChatBoxEn : ChatBox
{
    public ChatBoxEn()
    {
        IoCManager.InjectDependencies(this);
    }
}

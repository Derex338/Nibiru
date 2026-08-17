using Robust.Shared.Serialization;
using Robust.Shared.GameStates;

namespace Content.Shared._Nibiru.NPC.Utility;

[RegisterComponent, NetworkedComponent]
public sealed partial class NibiruPigeonPostComponent : Component
{
    [DataField("postName")]
    public string PostName = "Unknown Post";
}

[RegisterComponent, NetworkedComponent]
public sealed partial class NibiruBirdComponent : Component
{
    /// <summary>
    /// List of known post IDs.
    /// </summary>
    [DataField("knownPosts")]
    public List<EntityUid> KnownPosts = new();
}

[Serializable, NetSerializable]
public enum NibiruBirdDeliveryUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class NibiruBirdDeliveryUiState : BoundUserInterfaceState
{
    public readonly Dictionary<NetEntity, string> Posts;

    public NibiruBirdDeliveryUiState(Dictionary<NetEntity, string> posts)
    {
        Posts = posts;
    }
}

[Serializable, NetSerializable]
public sealed class NibiruBirdSelectPostMessage : BoundUserInterfaceMessage
{
    public readonly NetEntity Post;

    public NibiruBirdSelectPostMessage(NetEntity post)
    {
        Post = post;
    }
}

[Serializable, NetSerializable]
public enum NibiruRenamePostUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class NibiruRenamePostMessage : BoundUserInterfaceMessage
{
    public readonly string Name;

    public NibiruRenamePostMessage(string name)
    {
        Name = name;
    }
}

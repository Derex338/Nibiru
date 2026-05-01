using Robust.Shared.Serialization;
using Robust.Shared.GameStates;

namespace Content.Shared._Nibiru.NPC.Utility;

/// <summary>
/// Компонент для почтового отделения (голубятни).
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class NibiruPigeonPostComponent : Component
{
    [DataField("postName")]
    public string PostName = "Unknown Post";
}

/// <summary>
/// Компонент для птиц, способных доставлять почту.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class NibiruBirdComponent : Component
{
    /// <summary>
    /// Список ID известных почтовых отделений.
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

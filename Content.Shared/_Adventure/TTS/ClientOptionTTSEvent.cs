using Robust.Shared.Serialization;

namespace Content.Shared._Adventure.TTS;

[Serializable, NetSerializable]
public sealed class ClientOptionTTSEvent : EntityEventArgs
{
    public bool Enabled { get; }
    public ClientOptionTTSEvent(bool enabled)
    {
        Enabled = enabled;
    }
}

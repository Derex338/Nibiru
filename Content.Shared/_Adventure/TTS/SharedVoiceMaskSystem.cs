using Robust.Shared.Serialization;

namespace Content.Shared.VoiceMask;

public sealed class VoiceMaskChangeVoiceMessage : BoundUserInterfaceMessage
{
    public readonly string Voice;

    public VoiceMaskChangeVoiceMessage(string voice)
    {
        Voice = voice;
    }
}

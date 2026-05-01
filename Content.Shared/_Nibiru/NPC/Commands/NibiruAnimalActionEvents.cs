using Content.Shared.Actions;

namespace Content.Shared._Nibiru.NPC.Commands;

public sealed partial class NibiruAnimalFollowActionEvent : InstantActionEvent
{
    [DataField("speech")]
    public string? Speech;
}

public sealed partial class NibiruAnimalStayActionEvent : InstantActionEvent
{
    [DataField("speech")]
    public string? Speech;
}

public sealed partial class NibiruAnimalAttackActionEvent : EntityTargetActionEvent
{
    [DataField("speech")]
    public string? Speech;
}

public sealed partial class NibiruAnimalSearchActionEvent : EntityTargetActionEvent
{
    [DataField("speech")]
    public string? Speech;
}

public sealed partial class NibiruAnimalDeliverActionEvent : InstantActionEvent
{
    [DataField("speech")]
    public string? Speech;
}

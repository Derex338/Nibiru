using Content.Shared.Actions;
using Content.Shared._Nibiru.NPC.Training;

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

public sealed partial class NibiruAnimalAttackActionEvent : InstantActionEvent
{
    [DataField("speech")]
    public string? Speech;

    public NibiruAnimalCommand Type = NibiruAnimalCommand.Attack;
}

public sealed partial class NibiruAnimalGrabActionEvent : InstantActionEvent
{
    [DataField("speech")]
    public string? Speech;

    public NibiruAnimalCommand Type = NibiruAnimalCommand.Grab;
}

public sealed partial class NibiruAnimalSearchActionEvent : InstantActionEvent
{
    [DataField("speech")]
    public string? Speech;
}

public sealed partial class NibiruAnimalDeliverActionEvent : InstantActionEvent
{
    [DataField("speech")]
    public string? Speech;
}

public sealed partial class NibiruAnimalCommandModeEvent : InstantActionEvent
{
    [DataField("speech")]
    public string? Speech;

    public NibiruAnimalCommand CommandType;
}

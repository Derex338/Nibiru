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

// Теперь это toggle-действие, которое устанавливает режим
public sealed partial class NibiruAnimalAttackActionEvent : InstantActionEvent
{
    [DataField("speech")]
    public string? Speech;

    public NibiruAnimalCommand Type = NibiruAnimalCommand.Attack;
}

// Теперь это toggle-действие, которое устанавливает режим
public sealed partial class NibiruAnimalGrabActionEvent : InstantActionEvent
{
    [DataField("speech")]
    public string? Speech;

    public NibiruAnimalCommand Type = NibiruAnimalCommand.Grab;
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

// Событие для обработки команд, требующих указания цели
public sealed partial class NibiruAnimalCommandModeEvent : InstantActionEvent
{
    [DataField("speech")]
    public string? Speech;

    public NibiruAnimalCommand CommandType;
}

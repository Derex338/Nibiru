using Content.Shared._Nibiru.NPC.Training;
using Robust.Shared.GameStates;

namespace Content.Shared._Nibiru.NPC.Commands;

[RegisterComponent, NetworkedComponent]
public sealed partial class NibiruAnimalCommanderComponent : Component
{
    // Идентификаторы выдаваемых действий (Action)
    [DataField("followAction")]
    public string FollowActionId = "ActionNibiruAnimalFollow";

    [DataField("stayAction")]
    public string StayActionId = "ActionNibiruAnimalStay";

    [DataField("attackAction")]
    public string AttackActionId = "ActionNibiruAnimalAttack";

    [DataField("grabAction")]
    public string GrabActionId = "ActionNibiruAnimalGrab";

    [DataField("searchAction")]
    public string SearchActionId = "ActionNibiruAnimalSearch";

    [DataField("deliverAction")]
    public string DeliverActionId = "ActionNibiruAnimalDeliver";

    public EntityUid? CurrentAnimal;

    [DataField("currentMode")]
    public NibiruAnimalCommand? CurrentMode;

    public EntityUid? FollowActionEntity;
    public EntityUid? StayActionEntity;
    public EntityUid? AttackActionEntity;
    public EntityUid? GrabActionEntity;
    public EntityUid? SearchActionEntity;
    public EntityUid? DeliverActionEntity;
}

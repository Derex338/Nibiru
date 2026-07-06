using System;
using System.Collections.Generic;
using Robust.Shared.GameStates;

namespace Content.Shared._Nibiru.NPC.Behavior.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NibiruNpcMemoryComponent : Component
{
    [ViewVariables, AutoNetworkedField]
    public Dictionary<EntityUid, TimeSpan> HostileMemory = new();

    [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public float MemoryDuration = 30f;
}

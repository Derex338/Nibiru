using Content.Shared.Audio;
using Content.Shared.Damage;
using System.Numerics;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._Nibiru.NPC.Behavior;

[RegisterComponent]
public sealed partial class NibiruNpcBehaviorComponent : Component
{
    // Placeholder
}

[Serializable, NetSerializable]
public enum NibiruNpcState : byte
{
    Idle,

    Patrolling,

    Chasing,

    Attacking,

    Fleeing,

    Following,
    Returning,

    Hungry,

    Charging
}

[Serializable, NetSerializable]
public enum NibiruCombatStyle : byte
{
    Default,
    HitAndRun,
    Charge
}

using Robust.Shared.GameStates;

namespace Content.Shared._Nibiru.NPC.Utility;

/// <summary>
/// Component for a player holding a leashed animal.
/// Slows the player.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class NibiruLeashHolderComponent : Component
{
    [ViewVariables]
    public EntityUid LeashedAnimal;

    [DataField]
    public float WalkSpeedModifier = 0.85f;

    [DataField]
    public float SprintSpeedModifier = 0.85f;
}

/// <summary>
/// Component for a post/stake to which an animal can be tethered.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class NibiruLeashAnchorComponent : Component
{
}

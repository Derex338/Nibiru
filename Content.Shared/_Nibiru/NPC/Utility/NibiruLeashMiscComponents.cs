using Robust.Shared.GameStates;

namespace Content.Shared._Nibiru.NPC.Utility;

/// <summary>
/// Компонент для игрока, который ведёт на поводке животное.
/// Замедляет игрока.
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
/// Компонент для стойки/колышка, к которому можно привязать животное.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class NibiruLeashAnchorComponent : Component
{
}

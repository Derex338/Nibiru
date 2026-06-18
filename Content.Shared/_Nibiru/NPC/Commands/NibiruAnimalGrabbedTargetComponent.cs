using Robust.Shared.GameStates;

namespace Content.Shared._Nibiru.NPC.Commands;

/// <summary>
/// Компонент для цели, которую схватило животное.
/// Применяет замедление к цели.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NibiruAnimalGrabbedTargetComponent : Component
{
    /// <summary>
    /// Животное, которое схватило эту цель.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Grabber;

    /// <summary>
    /// Множитель замедления для цели.
    /// </summary>
    [DataField]
    public float SlowdownMultiplier = 0.6f;
}

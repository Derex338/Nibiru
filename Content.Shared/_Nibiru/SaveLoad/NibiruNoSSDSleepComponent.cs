using Robust.Shared.GameStates;

namespace Content.Shared._Nibiru.SaveLoad;

/// <summary>
/// Prevents the SSDIndicatorSystem from applying the ForcedSleep status effect.
/// This is used to avoid issues where saved characters might suffocate or die while waiting for the player.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class NibiruNoSSDSleepComponent : Component
{
}

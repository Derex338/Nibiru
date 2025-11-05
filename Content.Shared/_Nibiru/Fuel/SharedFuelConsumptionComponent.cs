using Content.Shared.Stacks;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Nibiru.Fuel;

[NetworkedComponent]
public abstract partial class SharedFuelConsumptionComponent : Component
{
    [ViewVariables(VVAccess.ReadOnly)]
    public FuelLightState CurrentState;

    [DataField]
    public string TurnOnBehaviourID = string.Empty;

    [DataField]
    public string FadeOutBehaviourID = string.Empty;

    [DataField]
    public TimeSpan GlowDuration = TimeSpan.FromSeconds(15f);

    [DataField]
    public TimeSpan FadeOutDuration = TimeSpan.FromSeconds(5f);

    [DataField]
    public float MaxFuelAmount = 500f;
	
	[DataField]
    public float FuelConsumption = 15f;

    [DataField]
    public SoundSpecifier? LitSound;

    [DataField]
    public SoundSpecifier? LoopedSound;

    [DataField]
    public SoundSpecifier? DieSound;
}

[Serializable, NetSerializable]
public enum FuelLightVisuals
{
    State,
    Behavior
}

[Serializable, NetSerializable]
public enum FuelLightState
{
    BrandNew,
    Lit,
    Fading,
    Dead
}
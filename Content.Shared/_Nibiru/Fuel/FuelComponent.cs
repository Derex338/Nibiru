using Content.Shared.Stacks;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Nibiru.Fuel;

[RegisterComponent, NetworkedComponent]
public sealed partial class FuelComponent : Component
{
    [DataField]
    public float Value = 100f;
	
	[DataField]
    public float TemperatureMax = 800f;
}

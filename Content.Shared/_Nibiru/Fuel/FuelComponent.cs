using Content.Shared.Stacks;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Nibiru.Fuel;

[NetworkedComponent]
public abstract partial class FuelComponent : Component
{
    [DataField]
    public int Value = 100;
	
	[DataField]
    public int TemperatureMax = 100;
}
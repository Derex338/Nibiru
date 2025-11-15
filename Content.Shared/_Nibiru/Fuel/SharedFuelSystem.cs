using System.Linq;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Content.Shared._Nibiru.Fuel;

namespace Content.Shared._Nibiru.Fuel;

public sealed partial class SharedFuelSystem : EntitySystem
{
    

    public override void Initialize()
    {
        base.Initialize();
        
    }
	/*
	public bool IsActive(<FuelConsumptionComponent?> entity)
    {
        if (!Resolve(entity.Owner, ref entity.Comp))
            return true;
		
		if(entity.Comp.CurrentState.Lit)
			return true;
		
		return false;
    }
	
	public float GetTemperatureEntity()
	{
		
	}*/
}
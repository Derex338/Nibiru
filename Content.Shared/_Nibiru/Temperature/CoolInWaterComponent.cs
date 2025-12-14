using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.DoAfter;
using Content.Shared.Temperature.Components;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Nibiru.Temperature;

[RegisterComponent, NetworkedComponent]
public sealed partial class CoolInWaterComponent : Component
{
    [DataField]
    public string? Solution;

    [DataField]
    public float CoolingRate = 1.0f;

    [DataField]
    public float MinTemperature = 423.15f; // 150 C

    [DataField]
    public float CoolingDelay = 1.0f;

    [DataField]
    public SoundSpecifier? CoolingSound;
}

public sealed class CoolInWaterSolutionEvent : EventArgs
{
    public float CurrentTemperature;
    public EntityUid uid;
    public Solution solution;

    public CoolInWaterSolutionEvent(float temp, EntityUid Uid, Solution Solution)
    {
        CurrentTemperature = temp;
        uid = Uid;
        solution = Solution;
    }
}

public sealed class CoolInWaterEntityEvent : EventArgs
{
    public float CurrentTemperature;
    public EntityUid uid;

    public CoolInWaterEntityEvent(float temp, EntityUid Uid)
    {
        CurrentTemperature = temp;
        uid = Uid;
    }
}

[Serializable, NetSerializable]
public sealed partial class CoolDoAfterEvent : SimpleDoAfterEvent
{
    public Solution solution;
    //public TemperatureComponent? temperatureComponent;

    public NetEntity TargetUid;

    public CoolDoAfterEvent(NetEntity uid, Solution sol)
    {
        TargetUid = uid;

        solution = sol;
        //temperatureComponent = TempComp;
    }
}

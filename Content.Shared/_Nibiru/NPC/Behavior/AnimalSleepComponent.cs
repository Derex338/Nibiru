using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Robust.Shared.Serialization;

namespace Content.Shared._Nibiru.NPC.Behavior;

[RegisterComponent]
public sealed partial class NibiruAnimalSleepComponent : Component
{
    [DataField]
    public float Energy = 600f;

    [DataField]
    public float MaxEnergy = 600f;

    [DataField]
    public float EnergyDrainRate = 1f;

    [DataField]
    public float EnergyRecoverRate = 1.0f;

    [DataField]
    public SleepCycle Cycle = SleepCycle.Diurnal;

    [DataField]
    public bool EnableProximityWake = false;

    [DataField]
    public float WakeProximityRadius = 2f;

    /// <summary>
    /// If an entity approaches with speed greater than this, the animal wakes up.
    /// </summary>
    [DataField]
    public float WakeProximitySpeedThreshold = 2.6f;

    [DataField]
    public EntProtoId? SleepVisualEffectPrototype = "AnimalZzzEffect";

    [DataField]
    public TimeSpan SleepVisualEffectInterval = TimeSpan.FromSeconds(2f);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan NextVisualEffectTime = TimeSpan.Zero;
}

[Serializable, NetSerializable]
public enum SleepCycle : byte
{
    Diurnal,   // Sleeps at night, awake at day
    Nocturnal  // Sleeps at day, awake at night
}

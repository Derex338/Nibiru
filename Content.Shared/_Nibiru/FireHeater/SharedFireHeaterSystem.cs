using Content.Shared.Examine;
using Content.Shared.Popups;
using Content.Shared.Power;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Temperature.Components;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Content.Shared._Nibiru.FireHeater;
using Content.Shared._Nibiru.Fuel;

namespace Content.Shared._Nibiru.FireHeater;

public abstract partial class SharedFireHeaterSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedPowerReceiverSystem _receiver = default!;
    [Dependency] private readonly SharedFuelSystem _fuel = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    //private readonly int _settingCount = Enum.GetValues<FireHeaterSetting>().Length;

    public override void Initialize()
    {
        base.Initialize();

        //SubscribeLocalEvent<FireHeaterComponent, FuelStateChangedEvent>(OnPowerChanged);
    }

    private void OnPowerChanged(Entity<FireHeaterComponent> ent, ref FuelStateChangedEvent args)
    {
		
    }
}

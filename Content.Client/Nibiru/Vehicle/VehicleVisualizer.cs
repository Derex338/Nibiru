using Content.Shared._Nibiru.Vehicle;
using Content.Shared.Movement.Components;
using Robust.Client.GameObjects;
using Robust.Shared.Serialization;

namespace Content.Client.Nibiru.Vehicle;

/// <summary>
/// Обрабатывает визуализацию транспорта (смена состояния спрайта)
/// </summary>
public sealed class VehicleVisualizerSystem : VisualizerSystem<VehicleVisualizerComponent>
{
    protected override void OnAppearanceChange(EntityUid uid, VehicleVisualizerComponent component,
        ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        var mounted = false;
        var dead = false;

        if (AppearanceSystem.TryGetData<bool>(uid, MountVisuals.Mounted, out var mountedValue, args.Component))
            mounted = mountedValue;

        if (AppearanceSystem.TryGetData<bool>(uid, MountVisuals.Dead, out var deadValue, args.Component))
            dead = deadValue;

        // Определяем какое состояние использовать
        string? state = component.BaseState;

        if (dead && component.DeadState != null)
            state = component.DeadState;
        else if (mounted && component.MountedState != null)
            state = component.MountedState;

        if (state != null && SpriteSystem.LayerExists((uid, args.Sprite), VehicleVisualLayers.Base))
            SpriteSystem.LayerSetRsiState((uid, args.Sprite), VehicleVisualLayers.Base, state);
    }
}

/// <summary>
/// Компонент для визуализации транспорта
/// </summary>
[RegisterComponent]
public sealed partial class VehicleVisualizerComponent : Component
{
    [DataField]
    public string? BaseState;

    [DataField]
    public string? MountedState;

    [DataField]
    public string? DeadState;
}

[Serializable]
public enum VehicleVisualLayers : byte
{
    Base
}

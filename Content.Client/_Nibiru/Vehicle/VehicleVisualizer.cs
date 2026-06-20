using Content.Shared.Movement.Components;
using Robust.Client.GameObjects;
using Robust.Shared.Serialization;

namespace Content.Client.Movement;

/// <summary>
/// Обрабатывает визуализацию транспорта (смена состояния спрайта)
/// </summary>
public sealed class RideableVisualizerSystem : VisualizerSystem<RideableVisualizerComponent>
{
    protected override void OnAppearanceChange(EntityUid uid, RideableVisualizerComponent component,
        ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        var mounted = false;
        var dead = false;

        if (AppearanceSystem.TryGetData<bool>(uid, RideableVisuals.Mounted, out var mountedValue, args.Component))
            mounted = mountedValue;

        if (AppearanceSystem.TryGetData<bool>(uid, RideableVisuals.Dead, out var deadValue, args.Component))
            dead = deadValue;

        // Определяем какое состояние использовать
        string? state = component.BaseState;

        if (dead && component.DeadState != null)
            state = component.DeadState;
        else if (mounted && component.MountedState != null)
            state = component.MountedState;

        if (state != null && SpriteSystem.LayerExists((uid, args.Sprite), RideableVisualLayers.Base))
            SpriteSystem.LayerSetRsiState((uid, args.Sprite), RideableVisualLayers.Base, state);
    }
}

/// <summary>
/// Компонент для визуализации транспорта
/// </summary>
[RegisterComponent]
public sealed partial class RideableVisualizerComponent : Component
{
    [DataField]
    public string? BaseState;

    [DataField]
    public string? MountedState;

    [DataField]
    public string? DeadState;
}

[Serializable]
public enum RideableVisualLayers : byte
{
    Base
}

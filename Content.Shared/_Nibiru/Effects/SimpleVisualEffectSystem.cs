using Content.Shared.Sprite;
using Robust.Shared.Timing;

namespace Content.Shared._Nibiru.Effects;

public sealed class SimpleVisualEffectSystem : EntitySystem
{
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<SimpleVisualEffectComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out var effect, out var xform))
        {
            if (effect.MoveRate != System.Numerics.Vector2.Zero)
            {
                xform.LocalPosition += effect.MoveRate * frameTime;
            }

            if (effect.ScaleRate != System.Numerics.Vector2.Zero)
            {
                var scaleSystem = EntityManager.System<SharedScaleVisualsSystem>();
                var currentScale = scaleSystem.GetSpriteScale(uid);
                var newScale = currentScale + effect.ScaleRate * frameTime;
                
                // Clamp to max scale
                newScale.X = MathF.Min(newScale.X, effect.MaxScale.X);
                newScale.Y = MathF.Min(newScale.Y, effect.MaxScale.Y);

                scaleSystem.SetSpriteScale(uid, newScale);
            }
        }
    }
}

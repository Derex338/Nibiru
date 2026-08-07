using System.Numerics;
using Content.Shared._CE.ZLevels.Light.Components;
using Content.Shared.Light.Components;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Prototypes;
using Content.Client.Graphics;
using Content.Client.Light;

namespace Content.Client._CE.ZLevels.Light;

public sealed partial class SunLightRayOverlay : Overlay
{
[Dependency] private IClyde _clyde = default!;
[Dependency] private IEntityManager _entManager = default!;
[Dependency] private IMapManager _mapManager = default!;
[Dependency] private IPrototypeManager _protoManager = default!;

    private readonly EntityLookupSystem _lookup;
    private readonly SharedTransformSystem _xformSys;

    private static readonly ProtoId<ShaderPrototype> MixShader = "Mix";

    public override OverlaySpace Space => OverlaySpace.BeforeLighting;

    private readonly HashSet<Entity<SunLightRayCastComponent>> _rays = new();
    private readonly OverlayResourceCache<CachedResources> _resources = new();
    private List<Entity<MapGridComponent>> _grids = new();

    public SunLightRayOverlay()
    {
        IoCManager.InjectDependencies(this);
        _xformSys = _entManager.System<SharedTransformSystem>();
        _lookup = _entManager.System<EntityLookupSystem>();
        // Draw after shadows or similar?
        // SunShadowOverlay has ZIndex = AfterLightTargetOverlay.ContentZIndex + 1;
        ZIndex = AfterLightTargetOverlay.ContentZIndex + 2;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var viewport = args.Viewport;
        var eye = viewport.Eye;

        if (eye == null)
            return;

        _grids.Clear();
        _mapManager.FindGridsIntersecting(args.MapId,
            args.WorldBounds.Enlarged(SunShadowComponent.MaxLength),
            ref _grids);

        if (_grids.Count == 0)
            return;

        var worldHandle = args.WorldHandle;
        var mapId = args.MapId;
        var worldBounds = args.WorldBounds;
        var targetSize = viewport.LightRenderTarget.Size;

        var res = _resources.GetForViewport(args.Viewport, static _ => new CachedResources());

        if (res.Target?.Size != targetSize)
        {
            res.Target = _clyde
                .CreateRenderTarget(targetSize,
                    new RenderTargetFormatParameters(RenderTargetColorFormat.Rgba8Srgb),
                    name: "sun-light-ray-target");

            if (res.BlurTarget?.Size != targetSize)
            {
                res.BlurTarget = _clyde
                    .CreateRenderTarget(targetSize, new RenderTargetFormatParameters(RenderTargetColorFormat.Rgba8Srgb), name: "sun-light-ray-blur");
            }
        }

        var lightScale = viewport.LightRenderTarget.Size / (Vector2)viewport.Size;
        var scale = viewport.RenderScale / (Vector2.One / lightScale);

        foreach (var grid in _grids)
        {
            if (!_entManager.TryGetComponent(grid.Owner, out SunLightRayComponent? lightRayComp))
                continue;

            // Try to get direction from SunShadowComponent first, then fallback to something
            Vector2 direction = Vector2.Zero;
            float alpha = 1.0f;

            if (_entManager.TryGetComponent(grid.Owner, out SunShadowComponent? sunShadow))
            {
                direction = sunShadow.Direction;
                alpha = sunShadow.Alpha;
            }

            if (direction.Equals(Vector2.Zero) || alpha <= 0.01f)
                continue;

            var expandedBounds = worldBounds.Enlarged(direction.Length() + 0.01f);
            _rays.Clear();
            _lookup.GetEntitiesIntersecting(mapId, expandedBounds, _rays);

            if (_rays.Count == 0)
                continue;

            // Determine Ray Color
            Color rayColor = Color.White;
            if (lightRayComp.Color.HasValue)
            {
                rayColor = lightRayComp.Color.Value;
            }
            else if (_entManager.TryGetComponent(grid.Owner, out MapLightComponent? mapLight))
            {
                rayColor = mapLight.AmbientLightColor;

                // If the current map's ambient is too dark, it's likely an indoor level.
                // We should try to find the "sun" color from the map above if possible.
                if (rayColor.R < 0.1f && rayColor.G < 0.1f && rayColor.B < 0.1f)
                {
                    // Fallback to white or try to find a better color if we had Z-level info here.
                    // For now, let's keep it as is or default to a "sun-like" color.
                    rayColor = new Color(1.0f, 0.95f, 0.8f); // Warm sun fallback
                }
            }

            // Draw ray polys to target
            args.WorldHandle.RenderInRenderTarget(res.Target,
                () =>
                {
                    var invMatrix = res.Target.GetWorldToLocalMatrix(eye, scale);
                    var indices = new Vector2[PhysicsConstants.MaxPolygonVertices * 2];

                    foreach (var ent in _rays)
                    {
                        var xform = _entManager.GetComponent<TransformComponent>(ent.Owner);
                        var (worldPos, worldRot) = _xformSys.GetWorldPositionRotation(xform);

                        var worldMatrix = Matrix3x2.CreateTranslation(worldPos);
                        var renderMatrix = Matrix3x2.Multiply(worldMatrix, invMatrix);
                        var pointCount = ent.Comp.Points.Length;

                        Array.Copy(ent.Comp.Points, indices, pointCount);

                        for (var i = 0; i < pointCount; i++)
                        {
                            indices[i] = worldRot.RotateVec(indices[i]);
                            indices[pointCount + i] = indices[i] + direction;
                        }

                        var points = PhysicsHull.ComputePoints(indices, pointCount * 2);
                        worldHandle.SetTransform(renderMatrix);
                        worldHandle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, points, Color.White);
                    }
                },
                Color.Transparent);

            // Blur it
            _clyde.BlurRenderTarget(viewport, res.Target, res.BlurTarget!, eye, 1.5f);

            // Draw to lighting render target
            args.WorldHandle.RenderInRenderTarget(viewport.LightRenderTarget,
                () =>
                {
                    var invMatrix = viewport.LightRenderTarget.GetWorldToLocalMatrix(eye, scale);
                    worldHandle.SetTransform(invMatrix);

                    var maskShader = _protoManager.Index(MixShader).Instance();
                    worldHandle.UseShader(maskShader);

                    // We use the ray color and the alpha from the sun shadow system
                    // Intensity multiplier from LightRayComponent
                    var finalAlpha = alpha * lightRayComp.Intensity;
                    worldHandle.DrawTextureRect(res.Target.Texture, worldBounds, rayColor.WithAlpha(finalAlpha));
                }, null);
        }
    }

    protected override void DisposeBehavior()
    {
        _resources.Dispose();
        base.DisposeBehavior();
    }

    private sealed class CachedResources : IDisposable
    {
        public IRenderTexture? BlurTarget;
        public IRenderTexture? Target;

        public void Dispose()
        {
            BlurTarget?.Dispose();
            Target?.Dispose();
        }
    }
}

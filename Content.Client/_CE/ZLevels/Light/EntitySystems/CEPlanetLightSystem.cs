using Robust.Client.Graphics;

namespace Content.Client._CE.ZLevels.Light.EntitySystems;

public sealed class CEPlanetLightSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlayMan = default!;

    public override void Initialize()
    {
        base.Initialize();
        _overlayMan.AddOverlay(new SunLightRayOverlay());
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlayMan.RemoveOverlay<SunLightRayOverlay>();
    }
}

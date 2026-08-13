using Robust.Client.Graphics;

namespace Content.Client._CE.ZLevels.Light.EntitySystems;

public sealed partial class CEPlanetLightSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlayMan = default!;

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

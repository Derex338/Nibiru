using Content.Client._Nibiru.NPC.UI;
using Robust.Client.Graphics;

namespace Content.Client._Nibiru.NPC.Systems;

public sealed partial class NibiruLeashSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlayManager = default!;

    public override void Initialize()
    {
        base.Initialize();
        
        _overlayManager.AddOverlay(new NibiruLeashOverlay(EntityManager));
    }

    public override void Shutdown()
    {
        base.Shutdown();
        
        _overlayManager.RemoveOverlay<NibiruLeashOverlay>();
    }
}

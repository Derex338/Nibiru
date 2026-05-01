using Content.Shared._Nibiru.NPC;
using Content.Shared._Nibiru.NPC.Utility;
using Robust.Client.Graphics;
using Robust.Shared.Enums;

namespace Content.Client._Nibiru.NPC.UI;

public sealed class NibiruLeashOverlay : Overlay
{
    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    private readonly IEntityManager _entManager;

    public NibiruLeashOverlay(IEntityManager entManager)
    {
        _entManager = entManager;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var query = _entManager.EntityQueryEnumerator<NibiruLeashableComponent>();
        var xformQuery = _entManager.GetEntityQuery<TransformComponent>();
        var worldHandle = args.WorldHandle;
        var xformSystem = _entManager.System<SharedTransformSystem>();

        while (query.MoveNext(out var uid, out var leash))
        {
            if (!leash.IsLeashed || leash.LeashedTo == null)
                continue;

            var target = leash.LeashedTo.Value;

            if (!xformQuery.TryGetComponent(target, out var targetXform) ||
                !xformQuery.TryGetComponent(uid, out var xform))
            {
                continue;
            }

            if (xform.MapID != targetXform.MapID)
                continue;

            var worldPos = xformSystem.GetWorldPosition(xform, xformQuery);
            var targetWorldPos = xformSystem.GetWorldPosition(targetXform, xformQuery);
            
            var diff = worldPos - targetWorldPos;
            if (diff.LengthSquared() < 0.01f)
                continue;

            var angle = diff.ToWorldAngle();
            var length = diff.Length() / 2f;
            var midPoint = targetWorldPos + diff / 2;
            const float Width = 0.04f;

            var box = new Box2(-Width, -length, Width, length);
            var rotated = new Box2Rotated(box.Translated(midPoint), angle, midPoint);

            // Цвет веревки (светло-коричневый)
            var color = Color.FromHex("#A0522D");

            worldHandle.DrawRect(rotated, color);
        }
    }
}

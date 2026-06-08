using Content.Shared._Nibiru.Factions;
using Content.Shared.IdentityManagement;
using Content.Shared.IdentityManagement.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Maths;
using System.Numerics;

namespace Content.Client._Nibiru.Factions.Systems;

public sealed partial class FactionIconOverlay : Overlay
{
    private readonly IEntityManager _entity;
    private readonly IPlayerManager _player;
    private readonly NibiruFactionLogoSystem _logoSystem;
    private readonly TransformSystem _transform;
    private readonly SpriteSystem _sprite;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    public FactionIconOverlay(IEntityManager entity, IPlayerManager player, NibiruFactionLogoSystem logoSystem, TransformSystem transform, SpriteSystem sprite)
    {
        _entity = entity;
        _player = player;
        _logoSystem = logoSystem;
        _transform = transform;
        _sprite = sprite;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        var eyeRot = args.Viewport.Eye?.Rotation ?? default;

        var localEntity = _player.LocalEntity;
        if (localEntity == null || !_entity.TryGetComponent<FactionComponent>(localEntity, out var localFaction))
            return;

        var localFactionName = localFaction.FactionName;
        if (string.IsNullOrEmpty(localFactionName))
            return;

        var texture = _logoSystem.GetFactionLogo8x8Texture(localFactionName);
        if (texture == null)
            return;

        var xformQuery = _entity.GetEntityQuery<TransformComponent>();
        var scaleMatrix = Matrix3Helpers.CreateScale(new Vector2(1, 1));
        var rotationMatrix = Matrix3Helpers.CreateRotation(-eyeRot);

        var query = _entity.AllEntityQueryEnumerator<FactionComponent, SpriteComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var sprite, out var xform))
        {
            if (xform.MapID != args.MapId || !sprite.Visible)
                continue;

            // Only show to members of the same faction
            if (comp.FactionName != localFactionName)
                continue;

            // Don't show on ourselves
            if (uid == localEntity)
                continue;

            // Don't show if identity is hidden
            var ev = new SeeIdentityAttemptEvent();
            _entity.EventBus.RaiseLocalEvent(uid, ev);
            if (ev.Cancelled)
                continue;

            var worldPos = _transform.GetWorldPosition(xform, xformQuery);
            var bounds = _sprite.GetLocalBounds((uid, sprite));

            if (!bounds.Translated(worldPos).Intersects(args.WorldAABB))
                continue;

            var worldMatrix = Matrix3Helpers.CreateTranslation(worldPos);
            var scaledWorld = Matrix3x2.Multiply(scaleMatrix, worldMatrix);
            var matty = Matrix3x2.Multiply(rotationMatrix, scaledWorld);
            handle.SetTransform(matty);

            // Calculate position over the head
            float yOffset = (bounds.Height + sprite.Offset.Y) / 2f + (8f / EyeManager.PixelsPerMeter); // slightly above head
            float xOffset = (sprite.Offset.X) - (4f / EyeManager.PixelsPerMeter); // centered

            var position = new Vector2(xOffset, yOffset);

            // Unshaded
            handle.UseShader(null);

            handle.DrawTexture(texture, position);

            handle.SetTransform(Matrix3x2.Identity);
        }
    }
}

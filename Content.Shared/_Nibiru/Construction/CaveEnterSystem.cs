using Content.Shared._Nibiru.GameTicking.Rules;
using Content.Shared.Ghost;
using Content.Shared.Interaction;
using Content.Shared.Maps;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Content.Shared.Teleportation.Components;
using Content.Shared.Teleportation.Systems;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Physics.Dynamics;
using Robust.Shared.Physics.Events;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using System.ComponentModel;
using System.Linq;
using System.Numerics;

namespace Content.Shared._Nibiru.Construction;

/// <summary>
/// Создание спуска и перемещение в пещеру.
/// </summary>
public sealed class SharedCaveEnterSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly INetManager _netMan = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly PullingSystem _pulling = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    private const string PortalFixture = "portalFixture";
    private const string ProjectileFixture = "projectile";

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<CaveEnterComponent, InteractHandEvent>(OnCollide);
        SubscribeLocalEvent<CaveEnterComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
    }

    private void OnGetVerbs(EntityUid uid, CaveEnterComponent component, GetVerbsEvent<AlternativeVerb> args)
    {
        // Traversal altverb for ghosts to use that bypasses normal functionality
        if (!args.CanAccess|| !HasComp<GhostComponent>(args.User))
            return;

        // Don't use the verb with unlinked or with multi-output portals
        // (this is only intended to be useful for ghosts to see where a linked portal leads)
        var disabled = !TryComp<LinkedEntityComponent>(uid, out var link) || link.LinkedEntities.Count != 1;

        args.Verbs.Add(new AlternativeVerb
        {
            Priority = 11,
            Act = () =>
            {
                if (link == null || disabled)
                    return;

                var ent = link.LinkedEntities.First();
                TeleportEntity(uid, args.User, Transform(ent).Coordinates, ent, false);
            },
            Disabled = disabled,
            Text = Loc.GetString("portal-component-ghost-traverse"),
            Message = disabled
                ? Loc.GetString("portal-component-no-linked-entities")
                : Loc.GetString("portal-component-can-ghost-traverse"),
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/open.svg.192dpi.png"))
        });
    }

    private void OnCollide(EntityUid uid, CaveEnterComponent component, ref InteractHandEvent args)
    {
        var subject = args.User;

        // best not.
        //if (Transform(subject).Anchored)
        //    return;

        // break pulls before portal enter so we dont break shit
        if (TryComp<PullableComponent>(subject, out var pullable) && pullable.BeingPulled)
        {
            _pulling.TryStopPull(subject, pullable);
        }

        if (TryComp<PullerComponent>(subject, out var pullerComp)
            && TryComp<PullableComponent>(pullerComp.Pulling, out var subjectPulling))
        {
            _pulling.TryStopPull(pullerComp.Pulling.Value, subjectPulling);
        }

        if (TryComp<LinkedEntityComponent>(uid, out var link))
        {
            if (link.LinkedEntities.Count == 0)
                return;

            // client can't predict outside of simple portal-to-portal interactions due to randomness involved
            // --also can't predict if the target doesn't exist on the client / is outside of PVS
            if (_netMan.IsClient)
            {
                var first = link.LinkedEntities.First();
                var exists = Exists(first);
                if (link.LinkedEntities.Count != 1 || !exists || (exists && Transform(first).MapID == MapId.Nullspace))
                    return;
            }

            // pick a target and teleport there
            var target = _random.Pick(link.LinkedEntities);

            TeleportEntity(uid, subject, Transform(target).Coordinates, target);
            return;
        }

        if (_netMan.IsClient)
            return;
    }

    public void TeleportEntity(EntityUid portal, EntityUid subject, EntityCoordinates target, EntityUid? targetEntity = null, bool playSound = true,
        CaveEnterComponent? portalComponent = null)
    {
        if (!Resolve(portal, ref portalComponent))
            return;

        var ourCoords = Transform(portal).Coordinates;
        var onSameMap = _transform.GetMapId(ourCoords) == _transform.GetMapId(target);
        //var distanceInvalid = ourCoords.TryDistance(EntityManager, target, out var distance);

        if (!onSameMap && !portalComponent.CanTeleportToOtherMaps)
        {
            if (!_netMan.IsServer)
                return;

            // Early out if this is an invalid configuration
            _popup.PopupCoordinates(Loc.GetString("portal-component-invalid-configuration-fizzle"),
                ourCoords, Filter.Pvs(ourCoords, entityMan: EntityManager), true);

            _popup.PopupCoordinates(Loc.GetString("portal-component-invalid-configuration-fizzle"),
                target, Filter.Pvs(target, entityMan: EntityManager), true);

            QueueDel(portal);

            if (targetEntity != null)
                QueueDel(targetEntity.Value);

            return;
        }

        var arrivalSound = CompOrNull<CaveEnterComponent>(targetEntity)?.ArrivalSound ?? portalComponent.ArrivalSound;
        var departureSound = portalComponent.DepartureSound;

        //LogTeleport(portal, subject, Transform(subject).Coordinates, target);

        _transform.SetCoordinates(subject, target);

        if (!playSound)
            return;

        _audio.PlayPredicted(departureSound, portal, subject);
        _audio.PlayPredicted(arrivalSound, subject, subject);
    }
}


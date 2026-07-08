using Content.Shared.Nutrition.Components;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
<<<<<<< Updated upstream
using Content.Shared.Tools.Systems;
using Content.Shared.Trigger.Components;
using Content.Shared.Trigger.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Player;
=======
using JetBrains.Annotations;
>>>>>>> Stashed changes

namespace Content.Shared.Nutrition.EntitySystems
{
    [UsedImplicitly]
    public abstract class SharedCreamPieSystem : EntitySystem
    {
        [Dependency] private SharedStunSystem _stunSystem = default!;
        [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

<<<<<<< Updated upstream
        SubscribeLocalEvent<CreamPieComponent, ThrowDoHitEvent>(OnCreamPieHit);
        SubscribeLocalEvent<CreamPieComponent, LandEvent>(OnCreamPieLand);
        SubscribeLocalEvent<CreamPiedComponent, ThrowHitByEvent>(OnCreamPiedHitBy);
        SubscribeLocalEvent<CreamPieComponent, BeforeToolRefinedEvent>(OnToolRefine);
        SubscribeLocalEvent<CreamPiedComponent, RejuvenateEvent>(OnRejuvenate);
    }

    /// <summary>
    /// SPLAT!
    /// </summary>
    public void SplatCreamPie(Entity<CreamPieComponent> creamPie)
    {
        // Already splatted! Do nothing.
        if (creamPie.Comp.Splatted)
            return;

        // The pie will be queued for deletion but there may be multiple collisions in the same tick, so we prevent it from splatting more than once.
        creamPie.Comp.Splatted = true;
        Dirty(creamPie);

        // The entity is being deleted, so play the sound at its position rather than parenting.
        if (_net.IsServer) // we don't have a user to pass in TODO: make the popup API sane and remove this guard
=======
        public override void Initialize()
>>>>>>> Stashed changes
        {
            base.Initialize();

            SubscribeLocalEvent<CreamPieComponent, ThrowDoHitEvent>(OnCreamPieHit);
            SubscribeLocalEvent<CreamPieComponent, LandEvent>(OnCreamPieLand);
            SubscribeLocalEvent<CreamPiedComponent, ThrowHitByEvent>(OnCreamPiedHitBy);
        }

        public void SplatCreamPie(Entity<CreamPieComponent> creamPie)
        {
            // Already splatted! Do nothing.
            if (creamPie.Comp.Splatted)
                return;

            creamPie.Comp.Splatted = true;

            SplattedCreamPie(creamPie);
        }

        protected virtual void SplattedCreamPie(Entity<CreamPieComponent, EdibleComponent?> entity) { }

        public void SetCreamPied(EntityUid uid, CreamPiedComponent creamPied, bool value)
        {
            if (value == creamPied.CreamPied)
                return;

            creamPied.CreamPied = value;

            if (TryComp(uid, out AppearanceComponent? appearance))
            {
                _appearance.SetData(uid, CreamPiedVisuals.Creamed, value, appearance);
            }
        }

        private void OnCreamPieLand(Entity<CreamPieComponent> entity, ref LandEvent args)
        {
            SplatCreamPie(entity);
        }

        private void OnCreamPieHit(Entity<CreamPieComponent> entity, ref ThrowDoHitEvent args)
        {
            SplatCreamPie(entity);
        }

        private void OnCreamPiedHitBy(EntityUid uid, CreamPiedComponent creamPied, ThrowHitByEvent args)
        {
            if (!Exists(args.Thrown) || !TryComp(args.Thrown, out CreamPieComponent? creamPie)) return;

            SetCreamPied(uid, creamPied, true);

            CreamedEntity(uid, creamPied, args);

<<<<<<< Updated upstream
    private void OnCreamPiedHitBy(Entity<CreamPiedComponent> creamPied, ref ThrowHitByEvent args)
    {
        if (!Exists(args.Thrown) || !TryComp<CreamPieComponent>(args.Thrown, out var creamPie))
            return;

        _stunSystem.TryUpdateParalyzeDuration(creamPied.Owner, creamPie.ParalyzeTime);

        // Already creamed, no need to spam popups.
        if (creamPied.Comp.CreamPied)
            return;

        // TODO: Check if they even have a head that can be hit.
        SetCreamPied(creamPied.AsNullable(), true);

        // Throwing is not predicted, so the thrower is not equal to the client predicting the collision, so we cannot pass in a user.
        // TODO: Make the popup API sane.
        if (_net.IsClient)
            return;

        // Shown only to the player that was hit.
        _popup.PopupEntity(
            Loc.GetString(
                "cream-pied-component-on-hit-by-message",
                ("thrown", args.Thrown)),
            creamPied.Owner, creamPied.Owner);

        var otherPlayers = Filter.PvsExcept(creamPied.Owner);

        // Show to everyone else.
        _popup.PopupEntity(
            Loc.GetString(
                "cream-pied-component-on-hit-by-message-others",
                ("owner", Identity.Entity(creamPied.Owner, EntityManager)),
                ("thrown", args.Thrown)),
            creamPied.Owner, otherPlayers, false);
    }

    private void OnRejuvenate(Entity<CreamPiedComponent> ent, ref RejuvenateEvent args)
    {
        SetCreamPied(ent.AsNullable(), false);
    }

    // TODO
    // A regression occured here. Previously creampies would activate their hidden payload if you tried to eat them.
    // However, the refactor to IngestionSystem caused the event to not be reached,
    // because eating is blocked if an item is inside the food.

    private void OnToolRefine(Entity<CreamPieComponent> ent, ref BeforeToolRefinedEvent args)
    {
        ActivatePayload(ent);
=======
            _stunSystem.TryUpdateParalyzeDuration(uid, TimeSpan.FromSeconds(creamPie.ParalyzeTime));
        }

        protected virtual void CreamedEntity(EntityUid uid, CreamPiedComponent creamPied, ThrowHitByEvent args) {}
>>>>>>> Stashed changes
    }
}

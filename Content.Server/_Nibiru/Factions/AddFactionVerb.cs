using Content.Server.Popups;
using Content.Shared.ActionBlocker;
using Content.Shared.Input;
using Content.Shared.Interaction;
using Content.Shared.Rotatable;
using Content.Shared.Verbs;
using Robust.Shared.Input.Binding;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Utility;
using Content.Shared.Humanoid;
using Content.Shared._Nibiru.Factions;
using Robust.Server.GameObjects;
using Content.Server.Mind;
using Content.Server.EUI;
using Content.Shared.IdentityManagement;
using Content.Server._Nibiru.Factions.UI;
//using Content.Client.Nibiru.Faction.UI;

namespace Content.Server._Nibiru.Factions
{
    public sealed class AddFactionVerb : EntitySystem
    {
        [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
        [Dependency] private readonly PopupSystem _popup = default!;
        [Dependency] private readonly MindSystem _mind = default!;
        [Dependency] private readonly EuiManager _euiMan = default!;
        [Dependency] private readonly ISharedPlayerManager _player = default!;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<HumanoidAppearanceComponent, GetVerbsEvent<AlternativeVerb>>(AddToFactionVerb);
        }

        private void AddToFactionVerb(EntityUid uid, HumanoidAppearanceComponent component, GetVerbsEvent<AlternativeVerb> args)
        {
            if (args.Hands == null || !args.CanAccess || !args.CanInteract || args.Target == args.User)
                return;

            if (!EntityManager.TryGetComponent<FactionComponent>(args.User, out var Leader) || !Leader.IsCreator)
                return;

            if (EntityManager.TryGetComponent<FactionComponent>(args.Target, out var TargetFact))
            {
                //if(string.IsNullOrWhiteSpace(TargetFact.FactionName) || string.IsNullOrWhiteSpace(Leader.FactionName) || Leader.FactionName == TargetFact.FactionName)
                return;
            }

            AlternativeVerb verb = new()
            {
                Text = Loc.GetString("Пригласить во фракцию"),
                Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/pray.svg.png")),
                Act = () => RequestJoinFaction(args.Target, args.User)
            };
            args.Verbs.Add(verb);
        }

        public void RequestJoinFaction(EntityUid target, EntityUid converter)
        {
            // Start conversion
            if (_mind.TryGetMind(target, out var consentMindId, out var mind) &&
                _player.TryGetSessionById(mind.UserId, out var session) &&
                !TryComp<FactionComponent>(target, out var targetComp) &&
                TryComp<FactionComponent>(converter, out var consFact))
            {
                // Tell the converter that request was sent
                _popup.PopupEntity(
                    Loc.GetString("ЗАПРОС ОТПРАВЛЕН", ("target", Identity.Entity(target, EntityManager))),
                    converter,
                    converter);

                var window = new FactionRequestedEui(target, converter, this, _popup, EntityManager);

                _euiMan.OpenEui(window, session);

                Dirty(converter, consFact);
                //Dirty(target, targetComp);
            }
            else
            {
                // Entity doesn't have mind (not controlled by player) to give response, but it's still convertable without it. We'll consent for them
                _popup.PopupEntity(
                    Loc.GetString("ОН БЕЗМОЗГЛЫЙ", ("target", Identity.Entity(target, EntityManager))),
                    converter,
                    converter);
            }
        }

        public void OnAccept(EntityUid target, EntityUid converter)
        {
            if (!TryComp<FactionComponent>(converter, out var consFact))
                return;

            var targetComp = AddComp<FactionComponent>(target);
            targetComp.FactionName = consFact.FactionName;
            targetComp.Leader = converter;
            targetComp.FactionColor = consFact.FactionColor;
            consFact.Members.Add(target);
            if (consFact.ResearchServer is not null)
                targetComp.ResearchServer = consFact.ResearchServer;

            Dirty(converter, consFact);
            Dirty(target, targetComp);
        }
    }
}

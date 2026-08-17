using Content.Server._Nibiru.World;
using Content.Server.Administration.Logs;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking.Rules;
using Content.Shared._Nibiru.GameTicking.Rules;
using Content.Shared.Database;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using JetBrains.Annotations;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Content.Server._Nibiru.Factions;
using Content.Shared._Nibiru.Factions;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Content.Shared.Preferences;

namespace Content.Server._Nibiru.GameTicking.Rules;

/// <summary>
/// Gamemode Nibiru Survival
/// </summary>
public sealed partial class NibiruSurvivalRuleSystem : GameRuleSystem<NibiruSurvivalRuleComponent>
{
    [Dependency] private NibiruWorldSystem _world = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IAdminLogManager _adminLogger = default!;
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private FactionSystem _factionSystem = default!;



    /// <summary>
    /// Factions list
    /// </summary>
    public readonly Dictionary<NetUserId, string?> PlayerFactionChoices = new();

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerBeforeSpawnEvent>(OnBeforeSpawn);
    }

    /// <summary>
    /// Save player's faction choice
    /// </summary>
    public void OnLateJoinFactionChoice(ICommonSession session, string? FactionName)
    {
        PlayerFactionChoices[session.UserId] = FactionName;
    }

    protected override void Added(EntityUid uid, NibiruSurvivalRuleComponent comp, GameRuleComponent gameRule, GameRuleAddedEvent args)
    {
        base.Added(uid, comp, gameRule, args);

        _world.InitializeWorld(comp);
    }

    private void OnBeforeSpawn(PlayerBeforeSpawnEvent ev)
    {
        var query = EntityQueryEnumerator<NibiruSurvivalRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var survivalComp, out var rule))
        {
            if (!GameTicker.IsGameRuleActive(uid, rule))
                continue;

            // First spawn player normally
            var entity = _world.SpawnPlayer(ev);

            // Then check if he chose a faction
            string? factionName = null;
            bool choiceMade = false;
            if (ev.Player.UserId is { } userId && PlayerFactionChoices.TryGetValue(userId, out var choice))
            {
                factionName = choice;
                PlayerFactionChoices.Remove(userId);
                choiceMade = true;
            }

            // If not selected through the selection grid, try to take from the profile
            if (!choiceMade && string.IsNullOrEmpty(factionName) && ev.Profile is HumanoidCharacterProfile profile)
            {
                if (!string.IsNullOrWhiteSpace(profile.FactionName))
                {
                    factionName = profile.FactionName;
                }
            }

            if (!string.IsNullOrEmpty(factionName) && entity != null)
            {
                _factionSystem.TryJoinPlayerToFaction(entity.Value, factionName);
            }

            ev.Handled = true;
            return;
        }
    }

    public NibiruSurvivalRuleComponent GetRule()
    {
        while (EntityQueryEnumerator<NibiruSurvivalRuleComponent>().MoveNext(out var comp))
        {
            return comp;
        }

        return EntityManager.ComponentFactory.GetComponent<NibiruSurvivalRuleComponent>();
    }

    public bool IsGameRuleActive(EntityUid ruleEntity, WorldRuleComponent? component = null)
    {
        return Resolve(ruleEntity, ref component) && HasComp<ActiveGameRuleComponent>(ruleEntity);
    }

    /// <summary>
    /// Returns the list of available factions for UI
    /// </summary>
    public IReadOnlyList<FactionInfo> GetAvailableFactions()
    {
        return _factionSystem.AvailableFactions;
    }
}

[ByRefEvent]
public readonly record struct WorldRuleAddedEvent(EntityUid RuleEntity, EntProtoId RuleId, EntityUid Target, EntityCoordinates TargetCoordinates);

[ByRefEvent]
public readonly record struct WorldRuleStartedEvent(EntityUid RuleEntity, EntProtoId RuleId, EntityUid Target, EntityCoordinates TargetCoordinates);

[ByRefEvent]
public readonly record struct WorldRuleEndedEvent(EntityUid RuleEntity, EntProtoId RuleId, EntityUid Target, EntityCoordinates TargetCoordinates);

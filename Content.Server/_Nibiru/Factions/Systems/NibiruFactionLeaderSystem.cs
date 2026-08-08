using Content.Server.GameTicking;
using Content.Shared._Nibiru.Factions;
using Content.Shared.Humanoid;
using Content.Shared.Preferences;
using Content.Shared.GameTicking;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking.Events;
using Robust.Shared.Network;
using Robust.Shared.Random;
using System.Linq;

namespace Content.Server._Nibiru.Factions;

public sealed partial class NibiruFactionLeaderSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private FactionSystem _factionSystem = default!;
    [Dependency] private IChatManager _chatManager = default!;
    // Список победителей лотереи, ожидающих спавна
    private readonly Dictionary<NetUserId, NibiruFactionLeaderPrefsMessage> _pendingLeaders = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RulePlayerSpawningEvent>(OnPlayerSpawning);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawned);
    }

    private void OnPlayerSpawning(RulePlayerSpawningEvent ev)
    {
        _pendingLeaders.Clear();
        var pendingPrefs = _factionSystem.PendingFactionLeaderPrefs;
        
        if (pendingPrefs.Count == 0)
            return;

        // Регистрируем ВСЕ фракции из лобби в реестре
        foreach (var pref in pendingPrefs.Values)
        {
            _factionSystem.RegisterLobbyPref(pref);
        }

        // Все, у кого заполнено название и кто в пуле спавна, становятся лидерами
        var poolIds = ev.PlayerPool.Select(p => p.UserId).ToHashSet();
        foreach (var (userId, pref) in pendingPrefs)
        {
            if (string.IsNullOrWhiteSpace(pref.FactionName) || !poolIds.Contains(userId))
                continue;

            _pendingLeaders[userId] = pref;
        }
    }

    private void OnPlayerSpawned(PlayerSpawnCompleteEvent ev)
    {
        var mob = ev.Mob;
        var userId = ev.Player.UserId;

        // Лидер фракции из лотереи
        if (_pendingLeaders.TryGetValue(userId, out var prefs))
        {
            var factionComp = EnsureComp<FactionComponent>(mob);
            factionComp.FactionName = prefs.FactionName;
            factionComp.Description = prefs.Description;
            factionComp.FactionColor = prefs.Color;
            factionComp.IconPath = prefs.IconPath;
            factionComp.IsRecruiting = prefs.IsRecruiting;
            factionComp.IsCreator = true;
            factionComp.Rank = Loc.GetString("faction-default-rank-leader");
            factionComp.Leader = mob;

            if (prefs.Logo16 != null)
                factionComp.LogoPixels = new List<Color>(prefs.Logo16);
            if (prefs.Logo8 != null)
                factionComp.LogoPixels8x8 = new List<Color>(prefs.Logo8);
            factionComp.LogoBackground = prefs.LogoBackground;

            if (prefs.FilterSpecies != null)
                factionComp.WhiteListSpecies = new List<string>(prefs.FilterSpecies);

            if (prefs.FilterGender == "Male")
                factionComp.WhiteListGender = new List<Sex> { Sex.Male };
            else if (prefs.FilterGender == "Female")
                factionComp.WhiteListGender = new List<Sex> { Sex.Female };
            else if (prefs.FilterGender == "Unsexed")
                factionComp.WhiteListGender = new List<Sex> { Sex.Unsexed };
            else
                factionComp.WhiteListGender = new List<Sex>();

            if (!string.IsNullOrWhiteSpace(prefs.FilterName))
                factionComp.WhiteListNames = prefs.FilterName
                    .Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                    .ToList();

            if (prefs.Roles != null && prefs.Roles.Count > 0)
                factionComp.Roles = new List<FactionRole>(prefs.Roles);

            Dirty(mob, factionComp);
            _factionSystem.RegisterFaction(factionComp);
            _pendingLeaders.Remove(userId);
            return;
        }

        // Обычный игрок с заполненными данными о фракции в лобби
        if (ev.Profile is not HumanoidCharacterProfile profile
            || string.IsNullOrWhiteSpace(profile.FactionName))
            return;

        var comp = EnsureComp<FactionComponent>(mob);
        comp.FactionName = profile.FactionName;
        comp.Description = profile.FactionDescription;

        var color = Color.TryFromHex(profile.FactionColor.AsSpan());
        if (color != null)
            comp.FactionColor = color.Value;

        comp.IconPath = profile.FactionIcon;
        comp.IsRecruiting = profile.FactionRecruiting;
        comp.IsCreator = true;
        comp.Rank = Loc.GetString("faction-default-rank-leader");
        comp.Leader = mob;

        if (profile.FactionLogo16 != null)
            comp.LogoPixels = new List<Color>(profile.FactionLogo16);
        if (profile.FactionLogo8 != null)
            comp.LogoPixels8x8 = new List<Color>(profile.FactionLogo8);
        comp.LogoBackground = profile.FactionLogoBackground;

        if (profile.FactionFilterSpecies != null)
            comp.WhiteListSpecies = new List<string>(profile.FactionFilterSpecies);

        if (profile.FactionFilterGender == "Male")
            comp.WhiteListGender = new List<Sex> { Sex.Male };
        else if (profile.FactionFilterGender == "Female")
            comp.WhiteListGender = new List<Sex> { Sex.Female };
        else if (profile.FactionFilterGender == "Unsexed")
            comp.WhiteListGender = new List<Sex> { Sex.Unsexed };
        else
            comp.WhiteListGender = new List<Sex>();

        if (!string.IsNullOrWhiteSpace(profile.FactionFilterName))
            comp.WhiteListNames = profile.FactionFilterName
                .Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .ToList();

        if (profile.FactionRoles != null && profile.FactionRoles.Count > 0)
            comp.Roles = new List<FactionRole>(profile.FactionRoles);

        Dirty(mob, comp);
        _factionSystem.RegisterFaction(comp);
    }
}

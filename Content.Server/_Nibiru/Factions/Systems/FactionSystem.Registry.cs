using Content.Shared._Nibiru.Factions;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Shared.Map.Components;
using Robust.Shared.Map;
using System.Linq;

namespace Content.Server._Nibiru.Factions;

public sealed partial class FactionSystem
{
    /// <summary>
    /// Обновляет список доступных фракций для клиентов
    /// </summary>
    public List<FactionInfo> UpdateAvailableFactionsList()
    {
        var factions = new List<FactionInfo>();

        // Проходим по всем картам
        var query = EntityQueryEnumerator<FactionRegistryComponent, MapComponent>();
        while (query.MoveNext(out var mapUid, out var registry, out _))
        {
            foreach (var (factionName, data) in registry.Factions)
            {
                // Конвертируем NetEntity обратно в EntityUid
                var leaderUid = GetEntity(data.Leader);

                // Считаем живых участников (включая лидера)
                var aliveCount = 0;
                if (_entityManager.EntityExists(leaderUid) &&
                    TryComp<MobStateComponent>(leaderUid, out var leaderMs) &&
                    leaderMs.CurrentState == MobState.Alive)
                {
                    aliveCount++;
                }

                var deadNetMembers = new List<NetEntity>();
                foreach (var netMember in data.Members)
                {
                    var memberUid = GetEntity(netMember);
                    if (!_entityManager.EntityExists(memberUid))
                    {
                        deadNetMembers.Add(netMember);
                        continue;
                    }
                    if (TryComp<MobStateComponent>(memberUid, out var ms) && ms.CurrentState == MobState.Alive)
                        aliveCount++;
                }

                // Удаляем несуществующих членов
                foreach (var dead in deadNetMembers)
                    data.Members.Remove(dead);

                if (aliveCount == 0 || !data.IsRecruiting)
                    continue;

                // Не добавляем дубликаты
                if (factions.Any(f => f.FactionName == factionName))
                    continue;

                factions.Add(new FactionInfo
                {
                    FactionName = factionName,
                    MemberCount = aliveCount,
                    Color = data.Color,
                    Description = data.Description,
                    IconPath = data.IconPath,
                    LogoBackground = data.LogoBackground,
                    LogoPixels = data.LogoPixels,
                    Status = data.Status,
                    IsRecruiting = data.IsRecruiting,
                    WhiteListSpecies = data.WhiteListSpecies,
                    WhiteListGender = data.WhiteListGender,
                    WhiteListSkinColor = data.WhiteListSkinColor,
                    WhiteListNames = data.WhiteListNames,
                    Leader = data.Leader,
                    Roles = data.Roles
                });
            }
        }

        AvailableFactions = factions;
        _broadcast.BroadcastFactionsList();

        return factions;
    }

    /// <summary>
    /// Регистрирует фракцию во всех реестрах карт
    /// </summary>
    public void RegisterFaction(FactionComponent faction)
    {
        if (string.IsNullOrWhiteSpace(faction.FactionName))
            return;

        var leaderNet = GetNetEntity(faction.Leader != default ? faction.Leader : faction.Owner);
        var membersNet = new List<NetEntity>();

        foreach (var member in faction.Members)
        {
            var netMember = GetNetEntity(member);
            if (netMember != leaderNet)
                membersNet.Add(netMember);
        }

        var mapQuery = EntityQueryEnumerator<MapComponent>();
        while (mapQuery.MoveNext(out var mapUid, out _))
        {
            var registry = EnsureComp<FactionRegistryComponent>(mapUid);
            registry.Factions[faction.FactionName] = new FactionRegistryData
            {
                Name = faction.FactionName,
                Leader = leaderNet,
                Members = membersNet,
                Color = faction.FactionColor,
                Description = faction.Description,
                IconPath = faction.IconPath,
                LogoBackground = faction.LogoBackground,
                LogoPixels = faction.LogoPixels,
                Status = faction.Status,
                IsRecruiting = faction.IsRecruiting,
                WhiteListSpecies = faction.WhiteListSpecies,
                WhiteListGender = faction.WhiteListGender,
                WhiteListSkinColor = faction.WhiteListSkinColor,
                WhiteListNames = faction.WhiteListNames,
                Created = _timing.CurTime,
                Roles = faction.Roles.Count > 0 ? faction.Roles : GetDefaultRoles()
            };

            if (faction.Roles.Count == 0)
                faction.Roles = GetDefaultRoles();


            Dirty(mapUid, registry);
        }

        Dirty(faction.Owner, faction);

        // Сразу обновляем список
        UpdateAvailableFactionsList();
    }

    /// <summary>
    /// Регистрирует фракцию из лобби в реестре
    /// </summary>
    public void RegisterLobbyPref(NibiruFactionLeaderPrefsMessage pref)
    {
        if (string.IsNullOrWhiteSpace(pref.FactionName))
            return;

        var query = EntityQueryEnumerator<MapComponent>();
        while (query.MoveNext(out var mapUid, out _))
        {
            var registry = EnsureComp<FactionRegistryComponent>(mapUid);

            // Если фракция уже есть (например, от победителя лотереи), не перезаписываем
            if (registry.Factions.ContainsKey(pref.FactionName))
                continue;

            registry.Factions[pref.FactionName] = new FactionRegistryData
            {
                Name = pref.FactionName,
                Leader = NetEntity.Invalid,
                Members = new List<NetEntity>(),
                Color = pref.Color,
                Description = pref.Description,
                IconPath = pref.IconPath,
                LogoBackground = Color.Transparent,
                LogoPixels = new(),
                Status = FactionStatus.Active,
                IsRecruiting = pref.IsRecruiting,
                WhiteListSpecies = new(),
                WhiteListGender = new(),
                WhiteListSkinColor = new(),
                WhiteListNames = new(),
                Roles = GetDefaultRoles()
            };
            Dirty(mapUid, registry);
        }
    }

    /// <summary>
    /// Обновляет данные фракции во всех реестрах
    /// </summary>
    private void UpdateFactionRegistry(FactionComponent faction)
    {
        var leaderNet = GetNetEntity(faction.Leader != default ? faction.Leader : faction.Owner);
        var membersNet = new List<NetEntity>();
        foreach (var member in faction.Members)
        {
            var netMember = GetNetEntity(member);
            if (netMember != leaderNet)
                membersNet.Add(netMember);
        }

        var query = EntityQueryEnumerator<FactionRegistryComponent>();
        while (query.MoveNext(out var mapEntity, out var registry))
        {
            if (!registry.Factions.ContainsKey(faction.FactionName))
                continue;

            var data = registry.Factions[faction.FactionName];
            data.Color = faction.FactionColor;
            data.Description = faction.Description;
            data.IconPath = faction.IconPath;
            data.LogoBackground = faction.LogoBackground;
            data.LogoPixels = faction.LogoPixels;
            data.Status = faction.Status;
            data.IsRecruiting = faction.IsRecruiting;
            data.WhiteListSpecies = faction.WhiteListSpecies;
            data.WhiteListGender = faction.WhiteListGender;
            data.WhiteListSkinColor = faction.WhiteListSkinColor;
            data.WhiteListNames = faction.WhiteListNames;
            data.Leader = leaderNet;
            data.Members = membersNet;
            data.Roles = faction.Roles;

            registry.Factions[faction.FactionName] = data;
            Dirty(mapEntity, registry);
        }

        UpdateMemberDataUI(faction.Owner, faction);

        // Обновляем список
        UpdateAvailableFactionsList();
    }

    /// <summary>
    /// Удаляет фракцию из всех реестров
    /// </summary>
    private void UnregisterFaction(string factionName)
    {
        var query = EntityQueryEnumerator<FactionRegistryComponent>();
        while (query.MoveNext(out var mapEntity, out var registry))
        {
            if (registry.Factions.Remove(factionName))
            {
                Dirty(mapEntity, registry);
            }
        }

        // Обновляем список
        UpdateAvailableFactionsList();
    }

    private void OnFactionStartup(EntityUid uid, FactionComponent component, ComponentStartup args)
    {
        if (!component.IsCreator)
            return;

        var xform = Transform(uid);
        if (xform.MapID == MapId.Nullspace)
            return;

        RegisterFaction(component);
    }

    private void OnFactionShutdown(EntityUid uid, FactionComponent component, ComponentShutdown args)
    {
        if (!component.IsCreator)
            return;

        var xform = Transform(uid);
        if (xform.MapID == MapId.Nullspace)
            return;

        UnregisterFaction(component.FactionName);
    }
}

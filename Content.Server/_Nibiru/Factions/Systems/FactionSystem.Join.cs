using System.Collections.Generic;
using Robust.Shared.Maths;
using Content.Shared._Nibiru.Factions;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Database;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Content.Shared.Humanoid;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._Nibiru.Factions;

public sealed partial class FactionSystem
{
    private void OnFactionCreateRequest(FactionCreateRequestMessage msg, EntitySessionEventArgs args)
    {
        var name = msg.FactionName?.Trim();
        if (string.IsNullOrWhiteSpace(name))
            return;

        var player = args.SenderSession.AttachedEntity;
        if (!player.HasValue)
            return;

        if (!ValidateFactionName(name, args.SenderSession.UserId, null, out var error))
        {
            _chatManager.DispatchServerMessage(args.SenderSession, error);
            return;
        }

        CreateFaction(player.Value, name);
    }

    private void CreateFaction(EntityUid player, string factionName)
    {
        if (!EntityManager.TryGetComponent<FactionComponent>(player, out var factionComponent))
        {
            factionComponent = EntityManager.AddComponent<FactionComponent>(player);

            factionComponent.FactionName = factionName;
            factionComponent.IsCreator = true;
            factionComponent.Rank = "Лидер";

            _adminLog.Add(LogType.FactionCreated, LogImpact.Medium,
                $"{ToPrettyString(player):player} создал фракцию с названием {factionName}");

            Dirty(player, factionComponent);

            RegisterFaction(factionComponent);
        }
    }

    private void OnFactionStateRequest(FactionStateRequestMessage msg, EntitySessionEventArgs args)
    {
        var player = args.SenderSession.AttachedEntity;

        if (!player.HasValue)
            return;

        if (EntityManager.TryGetComponent<FactionComponent>(player, out var factionComponent))
        {
            if (factionComponent.IsCreator == true)
                msg.Creator = true;

            msg.FactionName = factionComponent.FactionName;
        }
    }

    private void OnFactionLeaderPrefs(NibiruFactionLeaderPrefsMessage msg, EntitySessionEventArgs args)
    {
        var name = msg.FactionName?.Trim();
        if (string.IsNullOrWhiteSpace(name))
            return;

        if (!ValidateFactionName(name, args.SenderSession.UserId, null, out var error))
        {
            _chatManager.DispatchServerMessage(args.SenderSession, error);
            return;
        }

        msg.FactionName = name;
        msg.Description = msg.Description?.Trim() ?? "";
        if (msg.Description.Length > 500)
            msg.Description = msg.Description.Substring(0, 500);

        _pendingFactionLeaderPrefs[args.SenderSession.UserId] = msg;
    }

    /// <summary>
    /// Добавляет заспавненного игрока в выбранную фракцию
    /// </summary>
    public bool TryJoinPlayerToFaction(EntityUid playerEntity, string factionName)
    {
        // Ищем фракцию во всех реестрах
        var query = EntityQueryEnumerator<FactionRegistryComponent, MapComponent>();
        while (query.MoveNext(out var mapUid, out var registry, out _))
        {
            if (!registry.Factions.TryGetValue(factionName, out var factionData))
                continue;

            // Проверка фильтров
            if (!CheckFactionFilters(playerEntity, factionData, out var filterError))
            {
                _popup.PopupEntity(filterError, playerEntity, playerEntity);
                return false;
            }

            // Находим живого члена фракции для телепортации
            EntityUid? spawnNear = null;

            // Сначала проверяем лидера
            var leaderUid = GetEntity(factionData.Leader);
            if (_entityManager.EntityExists(leaderUid) &&
                TryComp<MobStateComponent>(leaderUid, out var leaderMob) &&
                leaderMob.CurrentState == MobState.Alive)
            {
                spawnNear = leaderUid;
            }
            else
            {
                // Ищем любого живого члена
                var deadNetMembers = new List<NetEntity>();
                foreach (var netMember in factionData.Members)
                {
                    var memberUid = GetEntity(netMember);
                    if (!_entityManager.EntityExists(memberUid))
                    {
                        deadNetMembers.Add(netMember);
                        continue;
                    }
                    if (TryComp<MobStateComponent>(memberUid, out var memberMob) &&
                        memberMob.CurrentState == MobState.Alive)
                    {
                        spawnNear = memberUid;
                        break;
                    }
                }

                // Удаляем несуществующих членов из данных реестра
                foreach (var dead in deadNetMembers)
                    factionData.Members.Remove(dead);
            }

            if (spawnNear != null)
            {
                // Телепортируем игрока рядом с членом фракции
                var targetXform = Transform(spawnNear.Value);
                var offset = _random.NextVector2(1f, 2f);
                var newCoords = targetXform.Coordinates.Offset(offset);

                _transform.SetCoordinates(playerEntity, newCoords);
            }
            else
            {
                // Если нет никого рядом, значит либо это первая фракция из лобби, либо все мертвы
                // Если в реестре еще нет лидера, то первый зашедший становится лидером
                if (leaderUid == EntityUid.Invalid && factionData.Members.Count == 0)
                {
                    leaderUid = playerEntity;
                }
            }

            // Добавляем в фракцию
            var playerFaction = EnsureComp<FactionComponent>(playerEntity);
            playerFaction.FactionName = factionName;
            playerFaction.Leader = leaderUid;
            playerFaction.FactionColor = factionData.Color;
            playerFaction.Description = factionData.Description;
            playerFaction.IconPath = factionData.IconPath;
            playerFaction.Status = factionData.Status;
            playerFaction.IsRecruiting = factionData.IsRecruiting;
            playerFaction.Roles = factionData.Roles;
            playerFaction.WhiteListSpecies = factionData.WhiteListSpecies;
            playerFaction.WhiteListGender = factionData.WhiteListGender;
            playerFaction.WhiteListSkinColors = factionData.WhiteListSkinColors;
            playerFaction.WhiteListNames = factionData.WhiteListNames;

            playerFaction.LogoBackground = factionData.LogoBackground;
            if (factionData.LogoPixels != null)
                playerFaction.LogoPixels = new List<Color>(factionData.LogoPixels);
            if (factionData.LogoPixels8x8 != null)
                playerFaction.LogoPixels8x8 = new List<Color>(factionData.LogoPixels8x8);

            if (leaderUid == playerEntity)
            {
                playerFaction.Rank = Loc.GetString("faction-rank-leader");
                playerFaction.IsCreator = true;
            }
            else
            {
                playerFaction.Rank = "Новобранец";
                playerFaction.IsCreator = false;
            }

            if (TryComp<FactionComponent>(leaderUid, out var leaderComp))
            {
                if (leaderUid != playerEntity && !leaderComp.Members.Contains(playerEntity))
                    leaderComp.Members.Add(playerEntity);

                Dirty(leaderUid, leaderComp);
                UpdateFactionRegistry(leaderComp);
            }

            Dirty(playerEntity, playerFaction);

            _popup.PopupEntity(
                Loc.GetString("faction-join-success", ("faction", factionName)),
                playerEntity,
                playerEntity);

            return true;
        }

        return false;
    }

    private bool CheckFactionFilters(EntityUid player, FactionRegistryData data, out string error)
    {
        error = string.Empty;

        // Проверка по расе
        if (data.WhiteListSpecies.Count > 0)
        {
            if (!TryComp<HumanoidAppearanceComponent>(player, out var appearance) ||
                !data.WhiteListSpecies.Contains(appearance.Species.Id))
            {
                error = Loc.GetString("faction-join-fail-species");
                return false;
            }
        }

        // Проверка по полу
        if (data.WhiteListGender.Count > 0)
        {
            if (!TryComp<HumanoidAppearanceComponent>(player, out var appearance) ||
                !data.WhiteListGender.Contains(appearance.Sex))
            {
                error = Loc.GetString("faction-join-fail-gender");
                return false;
            }
        }

        // Проверка по цвету кожи
        if (data.WhiteListSkinColors.Count > 0)
        {
            if (TryComp<HumanoidAppearanceComponent>(player, out var appearance))
            {
                if (data.WhiteListSkinColors.TryGetValue(appearance.Species.Id, out var skinFilter))
                {
                    var prototypeManager = Robust.Shared.IoC.IoCManager.Resolve<Robust.Shared.Prototypes.IPrototypeManager>();
                    var speciesProto = prototypeManager.Index<Content.Shared.Humanoid.Prototypes.SpeciesPrototype>(appearance.Species.Id);
                    var colorationProto = prototypeManager.Index<Content.Shared.Humanoid.SkinColorationPrototype>(speciesProto.SkinColoration);

                    if (colorationProto.Strategy.InputType == Content.Shared.Humanoid.SkinColorationStrategyInput.Unary)
                    {
                        var playerTone = colorationProto.Strategy.ToUnary(appearance.SkinColor);
                        var filterTone = colorationProto.Strategy.ToUnary(skinFilter.Color);

                        if (skinFilter.PassHigher && playerTone < filterTone)
                        {
                            error = Loc.GetString("faction-join-fail-skin-color");
                            return false;
                        }
                        if (!skinFilter.PassHigher && playerTone > filterTone)
                        {
                            error = Loc.GetString("faction-join-fail-skin-color");
                            return false;
                        }
                    }
                    else
                    {
                        var playerHsl = Color.ToHsl(appearance.SkinColor);
                        var filterHsl = Color.ToHsl(skinFilter.Color);

                        if (skinFilter.PassHigher && playerHsl.Z < filterHsl.Z)
                        {
                            error = Loc.GetString("faction-join-fail-skin-color");
                            return false;
                        }
                        if (!skinFilter.PassHigher && playerHsl.Z > filterHsl.Z)
                        {
                            error = Loc.GetString("faction-join-fail-skin-color");
                            return false;
                        }
                    }
                }
            }
        }

        // Проверка по имени
        if (data.WhiteListNames.Count > 0)
        {
            var name = Name(player);
            var passed = false;
            foreach (var keyword in data.WhiteListNames)
            {
                if (name.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    passed = true;
                    break;
                }
            }

            if (!passed)
            {
                error = Loc.GetString("faction-join-fail-name", ("word", string.Join(", ", data.WhiteListNames)));
                return false;
            }
        }

        return true;
    }
}

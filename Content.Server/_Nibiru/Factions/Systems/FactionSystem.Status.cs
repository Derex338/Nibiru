using Content.Shared._Nibiru.Factions;
using Content.Shared._Nibiru.Factions.Messeges;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using System.Linq;

namespace Content.Server._Nibiru.Factions;

public sealed partial class FactionSystem
{
    private void OnFactionStateChange(FactionChangeStateMessage msg, EntitySessionEventArgs args)
    {
        var player = args.SenderSession.AttachedEntity;

        if (!player.HasValue)
            return;

        if (!TryComp<FactionComponent>(player.Value, out var factionComponent)
            || !factionComponent.IsCreator)
        {
            _popup.PopupEntity(
                Loc.GetString("not-leader"),
                player.Value,
                player.Value);
            return;
        }

        bool needUpdate = false;

        if (msg.FactionName != null)
        {
            var name = msg.FactionName.Trim();
            if (name != factionComponent.FactionName)
            {
                if (!ValidateFactionName(name, args.SenderSession.UserId, factionComponent.FactionName, out var error))
                {
                    _chatManager.DispatchServerMessage(args.SenderSession, error);
                    return;
                }

                var oldName = factionComponent.FactionName;

                var regQuery = EntityQueryEnumerator<FactionRegistryComponent, MapComponent>();
                while (regQuery.MoveNext(out var mapEntity, out var reg, out _))
                {
                    if (reg.Factions.Remove(oldName, out var oldData))
                    {
                        oldData.Name = name;
                        reg.Factions[name] = oldData;
                        Dirty(mapEntity, reg);
                    }
                }

                factionComponent.FactionName = name;

                // Обновляем имя фракции у всех сущностей мира, а не только у тех кто в списке Members
                var allFactionQuery = EntityQueryEnumerator<FactionComponent>();
                while (allFactionQuery.MoveNext(out var entityUid, out var entityFaction))
                {
                    if (entityFaction.FactionName != oldName || entityUid == player.Value)
                        continue;

                    entityFaction.FactionName = name;
                    Dirty(entityUid, entityFaction);

                    // Уведомляем только игроков-людей (у кого есть MindComponent или похожее)
                    if (factionComponent.Members.Contains(entityUid))
                    {
                        _popup.PopupEntity(
                            Loc.GetString("faction-name-changed", ("factionName", name)),
                            entityUid,
                            entityUid);
                    }
                }

                needUpdate = true;
            }
        }

        if (msg.Description != null)
        {
            var desc = msg.Description.Trim();
            if (desc.Length > 500)
                desc = desc.Substring(0, 500);

            if (desc != factionComponent.Description)
            {
                factionComponent.Description = desc;
                foreach (var member in factionComponent.Members)
                {
                    if (TryComp<FactionComponent>(member, out var memberComp))
                    {
                        memberComp.Description = desc;
                        Dirty(member, memberComp);
                    }
                }
                needUpdate = true;
            }
        }

        if (msg.IconPath != null && msg.IconPath != factionComponent.IconPath)
        {
            factionComponent.IconPath = msg.IconPath;
            foreach (var member in factionComponent.Members)
            {
                if (TryComp<FactionComponent>(member, out var memberComp))
                {
                    memberComp.IconPath = msg.IconPath;
                    Dirty(member, memberComp);
                }
            }
            needUpdate = true;
        }

        if (msg.Color != null && msg.Color != factionComponent.FactionColor)
        {
            factionComponent.FactionColor = msg.Color.Value;
            foreach (var member in factionComponent.Members)
            {
                if (TryComp<FactionComponent>(member, out var memberComp))
                {
                    memberComp.FactionColor = msg.Color.Value;
                    Dirty(member, memberComp);
                }
            }
            needUpdate = true;
        }

        if (msg.Status != null && msg.Status != factionComponent.Status)
        {
            factionComponent.Status = msg.Status.Value;
            foreach (var member in factionComponent.Members)
            {
                if (TryComp<FactionComponent>(member, out var memberComp))
                {
                    memberComp.Status = msg.Status.Value;
                    Dirty(member, memberComp);
                }
            }
            needUpdate = true;
        }

        if (msg.IsRecruiting != null && msg.IsRecruiting != factionComponent.IsRecruiting)
        {
            factionComponent.IsRecruiting = msg.IsRecruiting.Value;
            foreach (var member in factionComponent.Members)
            {
                if (TryComp<FactionComponent>(member, out var memberComp))
                {
                    memberComp.IsRecruiting = msg.IsRecruiting.Value;
                    Dirty(member, memberComp);
                }
            }
            needUpdate = true;
        }

        // Новые фильтры
        if (msg.WhiteListSpecies != null)
        {
            factionComponent.WhiteListSpecies = msg.WhiteListSpecies;
            foreach (var member in factionComponent.Members)
            {
                if (TryComp<FactionComponent>(member, out var memberComp))
                {
                    memberComp.WhiteListSpecies = msg.WhiteListSpecies;
                    Dirty(member, memberComp);
                }
            }
            needUpdate = true;
        }

        if (msg.WhiteListGender != null)
        {
            factionComponent.WhiteListGender = msg.WhiteListGender;
            foreach (var member in factionComponent.Members)
            {
                if (TryComp<FactionComponent>(member, out var memberComp))
                {
                    memberComp.WhiteListGender = msg.WhiteListGender;
                    Dirty(member, memberComp);
                }
            }
            needUpdate = true;
        }

        if (msg.WhiteListSkinColors != null)
        {
            factionComponent.WhiteListSkinColors = msg.WhiteListSkinColors;
            foreach (var member in factionComponent.Members)
            {
                if (TryComp<FactionComponent>(member, out var memberComp))
                {
                    memberComp.WhiteListSkinColors = msg.WhiteListSkinColors;
                    Dirty(member, memberComp);
                }
            }
            needUpdate = true;
        }

        if (msg.WhiteListNames != null)
        {
            factionComponent.WhiteListNames = msg.WhiteListNames;
            foreach (var member in factionComponent.Members)
            {
                if (TryComp<FactionComponent>(member, out var memberComp))
                {
                    memberComp.WhiteListNames = msg.WhiteListNames;
                    Dirty(member, memberComp);
                }
            }
            needUpdate = true;
        }

        if (needUpdate)
        {
            Dirty(player.Value, factionComponent);
            UpdateFactionRegistry(factionComponent);
        }
    }

    private void OnLogoSave(NibiruFactionLogoSaveMessage msg, EntitySessionEventArgs args)
    {
        var player = args.SenderSession.AttachedEntity;

        if (!player.HasValue)
            return;

        if (!TryComp<FactionComponent>(player.Value, out var factionComponent)
            || !factionComponent.IsCreator)
        {
            _popup.PopupEntity(
                Loc.GetString("not-leader"),
                player.Value,
                player.Value);
            return;
        }

        factionComponent.LogoBackground = msg.BackgroundColor;
        factionComponent.LogoPixels = msg.Pixels;
        factionComponent.LogoPixels8x8 = msg.Pixels8x8;

        foreach (var member in factionComponent.Members)
        {
            if (TryComp<FactionComponent>(member, out var memberComp))
            {
                memberComp.LogoBackground = msg.BackgroundColor;
                memberComp.LogoPixels = msg.Pixels;
                memberComp.LogoPixels8x8 = msg.Pixels8x8;
                Dirty(member, memberComp);
            }
        }

        Dirty(player.Value, factionComponent);
        UpdateFactionRegistry(factionComponent);
    }
}

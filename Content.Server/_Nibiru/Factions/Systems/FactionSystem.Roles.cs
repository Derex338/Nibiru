using Content.Shared._Nibiru.Factions;

namespace Content.Server._Nibiru.Factions;

public sealed partial class FactionSystem
{
    private void OnCreateRole(FactionCreateRoleMessage msg, EntitySessionEventArgs args)
    {
        var player = args.SenderSession.AttachedEntity;

        if (!player.HasValue)
            return;

        if (!TryComp<FactionComponent>(player.Value, out var factionComponent)
            || !factionComponent.IsCreator)
            return;

        // If we are renaming
        if (!string.IsNullOrEmpty(msg.OldName))
        {
            var oldIndex = factionComponent.Roles.FindIndex(r => r.Name == msg.OldName);
            if (oldIndex >= 0)
            {
                factionComponent.Roles[oldIndex] = msg.Role;

                // Update members with old rank
                if (msg.OldName != msg.Role.Name)
                {
                    foreach (var memberUid in factionComponent.Members)
                    {
                        if (TryComp<FactionComponent>(memberUid, out var memberComp) && memberComp.Rank == msg.OldName)
                        {
                            memberComp.Rank = msg.Role.Name;
                            Dirty(memberUid, memberComp);
                        }
                    }
                }
            }
        }
        else
        {
            // Check if role already exists by name
            var existingIndex = factionComponent.Roles.FindIndex(r => r.Name == msg.Role.Name);
            if (existingIndex >= 0)
            {
                factionComponent.Roles[existingIndex] = msg.Role;
            }
            else
            {
                factionComponent.Roles.Add(msg.Role);
            }
        }

        Dirty(player.Value, factionComponent);
        UpdateFactionRegistry(factionComponent);
        UpdateMemberDataUI(player.Value, factionComponent);
    }

    private void OnDeleteRole(FactionDeleteRoleMessage msg, EntitySessionEventArgs args)
    {
        var player = args.SenderSession.AttachedEntity;

        if (!player.HasValue)
            return;

        if (!TryComp<FactionComponent>(player.Value, out var factionComponent)
            || !factionComponent.IsCreator)
            return;

        var index = factionComponent.Roles.FindIndex(r => r.Name == msg.RoleName);
        if (index == -1)
            return;

        factionComponent.Roles.RemoveAt(index);

        // Also reset rank for members who had this role
        foreach (var memberUid in factionComponent.Members)
        {
            if (TryComp<FactionComponent>(memberUid, out var memberComp) && memberComp.Rank == msg.RoleName)
            {
                memberComp.Rank = string.Empty;
                Dirty(memberUid, memberComp);
            }
        }

        Dirty(player.Value, factionComponent);
        UpdateFactionRegistry(factionComponent);
        UpdateMemberDataUI(player.Value, factionComponent);
    }
}

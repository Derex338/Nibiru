using System;
using Robust.Shared.Serialization;
using Content.Shared._Nibiru.Factions;
using Content.Shared.Humanoid;

namespace Content.Shared._Nibiru.Factions;

[Serializable, NetSerializable]
public sealed class FactionChangeStateMessage : EntityEventArgs
{
    public Color? Color = null;
    public string? FactionName = null;
    public string? Description = null;
    public string? IconPath = null;
    public FactionStatus? Status = null;
    public bool? IsRecruiting = null;
    public List<string>? WhiteListSpecies = null;
    public List<Sex>? WhiteListGender = null;
    public Dictionary<string, FactionSkinColorFilter>? WhiteListSkinColors = null;
    public List<string>? WhiteListNames = null;
}

/// <summary>
/// Сообщение для изменения ранга члена фракции
/// </summary>
[Serializable, NetSerializable]
public sealed class FactionChangeMemberRankMessage : EntityEventArgs
{
    public NetEntity Member = default;
    public string NewRank = string.Empty;
}

[Serializable, NetSerializable]
public sealed class FactionMoveMemberMessage : EntityEventArgs
{
    public NetEntity Member = default;
    public bool MoveUp = false;
}

[Serializable, NetSerializable]
public sealed class FactionCreateRoleMessage : EntityEventArgs
{
    public FactionRole Role = default;
    public string? OldName = null;
}

[Serializable, NetSerializable]
public sealed class FactionDeleteRoleMessage : EntityEventArgs
{
    public string RoleName = string.Empty;
}

/// <summary>
/// Сообщение от сервера с обновлённым списком фракций
/// Отправляется периодически (аналог LobbyJobsAvailableUpdated)
/// </summary>
[Serializable, NetSerializable]
public sealed class FactionsAvailableMessage : EntityEventArgs
{
    public List<FactionInfo> Factions { get; set; } = new();
}

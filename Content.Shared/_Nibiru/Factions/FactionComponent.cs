using Content.Shared.Construction.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Content.Shared.Humanoid;

namespace Content.Shared._Nibiru.Factions;

[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class FactionComponent : Component
{
    [AutoNetworkedField]
    [DataField("factionName")]
    public string FactionName { get; set; } = string.Empty;

    [AutoNetworkedField]
    [DataField("isCreator")]
    public bool IsCreator { get; set; } = false;

    /// <summary>
    /// All of the recipe packs that the faction type has by default
    /// </summary>
    [DataField]
    public List<ProtoId<ConstructionPackPrototype>> StaticPacks = new() { "FactionBase" };

    [ViewVariables]
    public EntityUid? ResearchServer;

    [AutoNetworkedField]
    [ViewVariables]
    public List<EntityUid> Members { get; set; } = new();

    /// <summary>
    /// Данные о членах фракции для отображения в UI (кэш для клиента)
    /// </summary>
    [AutoNetworkedField]
    public List<FactionMemberData> MemberData { get; set; } = new();

    [AutoNetworkedField]
    [ViewVariables]
    public EntityUid Leader = default!;

    [AutoNetworkedField]
    [ViewVariables]
    public EntityUid Heir = default!;

    [AutoNetworkedField]
    [ViewVariables]
    public Color FactionColor = Color.Pink;

    /// <summary>
    /// Описание фракции
    /// </summary>
    [AutoNetworkedField]
    [DataField("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Иконка фракции (путь к StatusIconPrototype)
    /// </summary>
    [AutoNetworkedField]
    [DataField("icon")]
    public string IconPath { get; set; } = string.Empty;

    /// <summary>
    /// Цвет фона логотипа фракции
    /// </summary>
    [AutoNetworkedField]
    [DataField("logoBackground")]
    public Color LogoBackground { get; set; } = Color.Transparent;

    /// <summary>
    /// Данные рисунка логотипа 32x32
    /// </summary>
    [AutoNetworkedField]
    [DataField("logoPixels")]
    public List<Color> LogoPixels { get; set; } = new();

    /// <summary>
    /// Ранг/должность члена фракции
    /// </summary>
    [AutoNetworkedField]
    [DataField("rank")]
    public string Rank { get; set; } = string.Empty;
    /// <summary>
    /// Статус фракции
    /// </summary>
    [AutoNetworkedField]
    [DataField("status")]
    public FactionStatus Status { get; set; } = FactionStatus.Active;

    /// <summary>
    /// Открыт ли набор в фракцию
    /// </summary>
    [AutoNetworkedField]
    [DataField("recruiting")]
    public bool IsRecruiting { get; set; } = false;

    /// <summary>
    /// Фильтр по расам (SpeciesPrototype)
    /// </summary>
    [AutoNetworkedField]
    [DataField("whiteListSpecies")]
    public List<string> WhiteListSpecies { get; set; } = new();

    /// <summary>
    /// Фильтр по полу (Sex)
    /// </summary>
    [AutoNetworkedField]
    [DataField("whiteListGender")]
    public List<Sex> WhiteListGender { get; set; } = new();

    /// <summary>
    /// Фильтр по цвету кожи для разных рас
    /// </summary>
    [AutoNetworkedField]
    [DataField("whiteListSkinColors")]
    public Dictionary<string, FactionSkinColorFilter> WhiteListSkinColors { get; set; } = new();

    /// <summary>
    /// Фильтр по словам в имени (через запятую)
    /// </summary>
    [AutoNetworkedField]
    [DataField("whiteListNames")]
    public List<string> WhiteListNames { get; set; } = new();

    /// <summary>
    /// Роли/Ранги фракции
    /// </summary>
    [AutoNetworkedField]
    [DataField("roles")]
    public List<FactionRole> Roles { get; set; } = new();
}

[Serializable, NetSerializable, DataDefinition]
public partial struct FactionRole
{
    [DataField("name")]
    public string Name;

    [DataField("canInvite")]
    public bool CanInvite;

    [DataField("canResearch")]
    public bool CanResearch;

    [DataField("canManageRoles")]
    public bool CanManageRoles;

    [DataField("canInherit")]
    public bool CanInherit;
}

/// <summary>
/// Фильтр цвета кожи
/// </summary>
[Serializable, NetSerializable, DataDefinition]
public partial struct FactionSkinColorFilter
{
    [DataField("color")]
    public Color Color;

    [DataField("passHigher")]
    public bool PassHigher;
}

/// <summary>
/// Данные о члене фракции для UI
/// </summary>
[Serializable, NetSerializable, DataDefinition]
public partial struct FactionMemberData
{
    [DataField("entity")]
    public NetEntity Entity;

    [DataField("name")]
    public string Name;

    [DataField("rank")]
    public string Rank;
}

/// <summary>
/// Компонент для хранения всех фракций на карте
/// Прикрепляется к entity карты
/// </summary>
[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class FactionRegistryComponent : Component
{
    /// <summary>
    /// Все зарегистрированные фракции на карте
    /// Ключ - название фракции, значение - данные о фракции
    /// </summary>
    [AutoNetworkedField]
    [DataField("factions")]
    public Dictionary<string, FactionRegistryData> Factions { get; set; } = new();
}

/// <summary>
/// Данные о фракции в реестре
/// </summary>
[Serializable, NetSerializable]
[DataDefinition]
public partial struct FactionRegistryData
{
    /// <summary>
    /// Название фракции
    /// </summary>
    [DataField("name")]
    public string Name;

    /// <summary>
    /// Лидер фракции (сериализуемый)
    /// </summary>
    [DataField("leader")]
    public NetEntity Leader;

    /// <summary>
    /// Список всех членов фракции (сериализуемый)
    /// </summary>
    [DataField("members")]
    public List<NetEntity> Members;

    /// <summary>
    /// Цвет фракции
    /// </summary>
    [DataField("color")]
    public Color Color;

    /// <summary>
    /// Описание фракции
    /// </summary>
    [DataField("description")]
    public string Description;

    /// <summary>
    /// Путь к иконке
    /// </summary>
    [DataField("icon")]
    public string IconPath;

    /// <summary>
    /// Цвет фона логотипа фракции
    /// </summary>
    [DataField("logoBackground")]
    public Color LogoBackground;

    /// <summary>
    /// Данные рисунка логотипа 32x32
    /// </summary>
    [DataField("logoPixels")]
    public List<Color> LogoPixels;

    /// <summary>
    /// Статус фракции
    /// </summary>
    [DataField("status")]
    public FactionStatus Status;

    /// <summary>
    /// Открыт ли набор
    /// </summary>
    [DataField("recruiting")]
    public bool IsRecruiting;

    /// <summary>
    /// Время создания фракции
    /// </summary>
    [DataField("created")]
    public TimeSpan Created;

    /// <summary>
    /// Фильтры для вступления
    /// </summary>
    [DataField("whiteListSpecies")]
    public List<string> WhiteListSpecies;

    [DataField("whiteListGender")]
    public List<Sex> WhiteListGender;

    [DataField("whiteListSkinColors")]
    public Dictionary<string, FactionSkinColorFilter> WhiteListSkinColors;

    [DataField("whiteListNames")]
    public List<string> WhiteListNames;

    /// <summary>
    /// Список ролей фракции
    /// </summary>
    [DataField("roles")]
    public List<FactionRole> Roles;
}

/// <summary>
/// Статус фракции
/// </summary>
[Serializable, NetSerializable]
public enum FactionStatus : byte
{
    Active,      // Активна
    Recruiting,  // Набирает членов
    AtWar        // В состоянии войны
}

/// <summary>
/// Информация о фракции для UI
/// </summary>
[Serializable, NetSerializable]
public sealed class FactionInfo
{
    public string FactionName { get; set; } = string.Empty;
    public int MemberCount { get; set; }
    public Color Color { get; set; } = Color.White;
    public string Description { get; set; } = string.Empty;
    public string IconPath { get; set; } = string.Empty;
    public Color LogoBackground { get; set; } = Color.Transparent;
    public List<Color> LogoPixels { get; set; } = new();
    public FactionStatus Status { get; set; } = FactionStatus.Active;
    public bool IsRecruiting { get; set; } = false;
    public List<string> WhiteListSpecies { get; set; } = new();
    public List<Sex> WhiteListGender { get; set; } = new();
    public Dictionary<string, FactionSkinColorFilter> WhiteListSkinColors { get; set; } = new();
    public List<string> WhiteListNames { get; set; } = new();
    public NetEntity Leader { get; set; }
    public List<FactionRole> Roles { get; set; } = new();
}

/// <summary>
/// Сообщение для запроса списка фракций
/// </summary>
[Serializable, NetSerializable]
public sealed class RequestFactionsMessage : EntityEventArgs
{
}

/// <summary>
/// Сообщение с списком доступных фракций
/// </summary>
[Serializable, NetSerializable]
public sealed class AvailableFactionsMessage : EntityEventArgs
{
    public List<FactionInfo> Factions { get; set; } = new();
}

/// <summary>
/// Сообщение для присоединения к фракции через поздний вход
/// </summary>
[Serializable, NetSerializable]
public sealed class LateJoinFactionMessage : EntityEventArgs
{
    public string? FactionName { get; set; }
}

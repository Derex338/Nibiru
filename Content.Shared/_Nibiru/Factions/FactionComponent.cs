using Content.Shared.Construction.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

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
    public List<ProtoId<ConstructionPackPrototype>> StaticPacks = new();

    [ViewVariables]
    public EntityUid? ResearchServer;

    [AutoNetworkedField]
    [ViewVariables]
    public List<EntityUid> Members { get; set; } = new();

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
    public FactionStatus Status { get; set; } = FactionStatus.Active;
    public bool IsRecruiting { get; set; } = false;
    public NetEntity Leader { get; set; }
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

namespace Content.Shared._Nibiru.NPC.Behavior;

/// <summary>
/// Определяет базовый тип поведения NPC по отношению к другим существам.
/// </summary>
public enum NibiruNpcBehaviorType : byte
{
    /// <summary>
    /// Нападает на враждебных существ при обнаружении.
    /// </summary>
    Aggressive,

    /// <summary>
    /// Атакует только в ответ на агрессию или при выполнении определённых условий.
    /// </summary>
    Neutral,

    /// <summary>
    /// Никогда не атакует. Убегает при получении урона.
    /// </summary>
    Passive,

    /// <summary>
    /// Убегает от игроков и угроз при их обнаружении в зоне видимости.
    /// </summary>
    Shy
}

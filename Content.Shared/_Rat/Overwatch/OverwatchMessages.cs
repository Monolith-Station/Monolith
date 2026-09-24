using Robust.Shared.Serialization;

namespace Content.Shared._Rat.Overwatch;

/// <summary>
/// Запрос на обновление списка членов фракции.
/// </summary>
[Serializable, NetSerializable]
public sealed class OverwatchRefreshMessage : BoundUserInterfaceMessage
{
}

/// <summary>
/// Состояние UI с данными о членах фракции.
/// </summary>
[Serializable, NetSerializable]
public sealed class OverwatchUpdateState : BoundUserInterfaceState
{
    /// <summary>
    /// Список данных о членах фракции.
    /// </summary>
    public readonly List<OverwatchMemberData> Members;

    /// <summary>
    /// Доступные отряды (ID -> Название).
    /// </summary>
    public readonly Dictionary<int, string> AvailableSquads;

    /// <summary>
    /// Текущий фильтр по статусу.
    /// </summary>
    public readonly OverwatchMemberStatus? StatusFilter;

    /// <summary>
    /// Текущий фильтр по отряду.
    /// </summary>
    public readonly int? SquadFilter;

    /// <summary>
    /// Текущий поисковый запрос.
    /// </summary>
    public readonly string SearchQuery;

    /// <summary>
    /// Цвет фракции для UI.
    /// </summary>
    public readonly Color FactionColor;

    public OverwatchUpdateState(
        List<OverwatchMemberData> members,
        Dictionary<int, string>? availableSquads = null,
        OverwatchMemberStatus? statusFilter = null,
        int? squadFilter = null,
        string searchQuery = "",
        Color factionColor = default)
    {
        Members = members;
        AvailableSquads = availableSquads ?? new Dictionary<int, string>();
        StatusFilter = statusFilter;
        SquadFilter = squadFilter;
        SearchQuery = searchQuery;
        FactionColor = factionColor;
    }
}

/// <summary>
/// Запрос на просмотр камеры цели.
/// </summary>
[Serializable, NetSerializable]
public sealed class OverwatchViewCameraMessage : BoundUserInterfaceMessage
{
    /// <summary>
    /// Сущность цели для наблюдения.
    /// </summary>
    public readonly NetEntity Target;

    public OverwatchViewCameraMessage(NetEntity target)
    {
        Target = target;
    }
}

/// <summary>
/// Запрос на остановку наблюдения.
/// </summary>
[Serializable, NetSerializable]
public sealed class OverwatchStopWatchingMessage : BoundUserInterfaceMessage
{
}

/// <summary>
/// Запрос на установку фильтра по статусу.
/// </summary>
[Serializable, NetSerializable]
public sealed class OverwatchSetStatusFilterMessage : BoundUserInterfaceMessage
{
    /// <summary>
    /// Статус для фильтрации.
    /// </summary>
    public readonly OverwatchMemberStatus? Status;

    public OverwatchSetStatusFilterMessage(OverwatchMemberStatus? status)
    {
        Status = status;
    }
}

/// <summary>
/// Запрос на установку фильтра по отряду.
/// </summary>
[Serializable, NetSerializable]
public sealed class OverwatchSetSquadFilterMessage : BoundUserInterfaceMessage
{
    /// <summary>
    /// ID отряда для фильтрации.
    /// </summary>
    public readonly int? SquadId;

    public OverwatchSetSquadFilterMessage(int? squadId)
    {
        SquadId = squadId;
    }
}

/// <summary>
/// Запрос на установку поискового запроса.
/// </summary>
[Serializable, NetSerializable]
public sealed class OverwatchSetSearchMessage : BoundUserInterfaceMessage
{
    /// <summary>
    /// Строка поискового запроса.
    /// </summary>
    public readonly string SearchQuery;

    public OverwatchSetSearchMessage(string searchQuery)
    {
        SearchQuery = searchQuery;
    }
}

/// <summary>
/// Запрос на создание нового отряда.
/// </summary>
[Serializable, NetSerializable]
public sealed class OverwatchCreateSquadMessage : BoundUserInterfaceMessage
{
    /// <summary>
    /// Название нового отряда.
    /// </summary>
    public readonly string SquadName;

    public OverwatchCreateSquadMessage(string squadName)
    {
        SquadName = squadName;
    }
}

/// <summary>
/// Запрос на удаление отряда.
/// </summary>
[Serializable, NetSerializable]
public sealed class OverwatchDeleteSquadMessage : BoundUserInterfaceMessage
{
    /// <summary>
    /// ID отряда для удаления.
    /// </summary>
    public readonly int SquadId;

    public OverwatchDeleteSquadMessage(int squadId)
    {
        SquadId = squadId;
    }
}

/// <summary>
/// Запрос на назначение игрока в отряд.
/// </summary>
[Serializable, NetSerializable]
public sealed class OverwatchAssignSquadMessage : BoundUserInterfaceMessage
{
    /// <summary>
    /// Сущность игрока.
    /// </summary>
    public readonly NetEntity Player;

    /// <summary>
    /// ID отряда для назначения.
    /// </summary>
    public readonly int SquadId;

    public OverwatchAssignSquadMessage(NetEntity player, int squadId)
    {
        Player = player;
        SquadId = squadId;
    }
}

/// <summary>
/// Запрос на удаление игрока из отряда.
/// </summary>
[Serializable, NetSerializable]
public sealed class OverwatchRemoveSquadMemberMessage : BoundUserInterfaceMessage
{
    /// <summary>
    /// Сущность игрока.
    /// </summary>
    public readonly NetEntity Player;

    public OverwatchRemoveSquadMemberMessage(NetEntity player)
    {
        Player = player;
    }
}

/// <summary>
/// Запрос на отправку объявления.
/// </summary>
[Serializable, NetSerializable]
public sealed class OverwatchSendMessageAnnouncement : BoundUserInterfaceMessage
{
    /// <summary>
    /// Текст объявления.
    /// </summary>
    public readonly string Message;

    /// <summary>
    /// ID целевого отряда или null для всей фракции.
    /// </summary>
    public readonly int? TargetSquadId;

    public OverwatchSendMessageAnnouncement(string message, int? targetSquadId)
    {
        Message = message;
        TargetSquadId = targetSquadId;
    }
}

/// <summary>
/// Событие для отображения объявления Overwatch.
/// </summary>
[Serializable, NetSerializable]
public sealed class OverwatchAnnouncementEvent : EntityEventArgs
{
    /// <summary>
    /// Текст объявления.
    /// </summary>
    public readonly string Message;

    /// <summary>
    /// Название получателя (фракция или отряд).
    /// </summary>
    public readonly string TargetName;

    /// <summary>
    /// Название Overwatch для фракции.
    /// </summary>
    public readonly string OverwatchTitle;

    /// <summary>
    /// Цвет текста объявления.
    /// </summary>
    public readonly Color Color;

    public OverwatchAnnouncementEvent(string message, string targetName, string overwatchTitle, Color color)
    {
        Message = message;
        TargetName = targetName;
        OverwatchTitle = overwatchTitle;
        Color = color;
    }
}

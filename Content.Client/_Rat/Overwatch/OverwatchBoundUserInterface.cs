using Content.Shared._Rat.Overwatch;
using Robust.Client.UserInterface;

namespace Content.Client._Rat.Overwatch;

/// <summary>
/// BUI для консоли Overwatch.
/// </summary>
public sealed class OverwatchBoundUserInterface : BoundUserInterface
{
    private OverwatchWindow? _window;

    public OverwatchBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    /// <inheritdoc/>
    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<OverwatchWindow>();
        _window.Initialize(this);

        _window.OnClose += () =>
        {
            _window = null;
        };
    }

    /// <inheritdoc/>
    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is OverwatchUpdateState updateState)
        {
            _window?.UpdateState(updateState);
        }
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        _window?.Dispose();
        _window = null;
        base.Dispose(disposing);
    }

    /// <summary>
    /// Запрашивает переключение вида на камеру цели.
    /// </summary>
    /// <param name="target">Сущность цели для наблюдения.</param>
    public void ViewCamera(NetEntity target)
    {
        SendMessage(new OverwatchViewCameraMessage(target));
    }

    /// <summary>
    /// Запрашивает остановку текущего наблюдения.
    /// </summary>
    public void StopWatching()
    {
        SendMessage(new OverwatchStopWatchingMessage());
    }

    /// <summary>
    /// Запрашивает установку фильтра по статусу участника.
    /// </summary>
    /// <param name="status">Статус для фильтрации или null для сброса.</param>
    public void SetStatusFilter(OverwatchMemberStatus? status)
    {
        SendMessage(new OverwatchSetStatusFilterMessage(status));
    }

    /// <summary>
    /// Запрашивает установку фильтра по отряду.
    /// </summary>
    /// <param name="squadId">ID отряда для фильтрации или null для сброса.</param>
    public void SetSquadFilter(int? squadId)
    {
        SendMessage(new OverwatchSetSquadFilterMessage(squadId));
    }

    /// <summary>
    /// Запрашивает установку поискового запроса.
    /// </summary>
    /// <param name="query">Строка поискового запроса.</param>
    public void SetSearchQuery(string query)
    {
        SendMessage(new OverwatchSetSearchMessage(query));
    }

    /// <summary>
    /// Запрашивает создание нового отряда.
    /// </summary>
    /// <param name="squadName">Название нового отряда.</param>
    public void CreateSquad(string squadName)
    {
        SendMessage(new OverwatchCreateSquadMessage(squadName));
    }

    /// <summary>
    /// Запрашивает удаление отряда.
    /// </summary>
    /// <param name="squadId">ID отряда для удаления.</param>
    public void DeleteSquad(int squadId)
    {
        SendMessage(new OverwatchDeleteSquadMessage(squadId));
    }

    /// <summary>
    /// Запрашивает назначение игрока в отряд.
    /// </summary>
    /// <param name="player">Сущность игрока.</param>
    /// <param name="squadId">ID отряда для назначения.</param>
    public void AssignSquad(NetEntity player, int squadId)
    {
        SendMessage(new OverwatchAssignSquadMessage(player, squadId));
    }

    /// <summary>
    /// Запрашивает удаление игрока из отряда.
    /// </summary>
    /// <param name="player">Сущность игрока.</param>
    public void RemoveSquadMember(NetEntity player)
    {
        SendMessage(new OverwatchRemoveSquadMemberMessage(player));
    }

    /// <summary>
    /// Запрашивает отправку объявления.
    /// </summary>
    /// <param name="message">Текст объявления.</param>
    /// <param name="targetSquadId">ID целевого отряда или null для всей фракции.</param>
    public void SendAnnouncement(string message, int? targetSquadId = null)
    {
        SendMessage(new OverwatchSendMessageAnnouncement(message, targetSquadId));
    }
}

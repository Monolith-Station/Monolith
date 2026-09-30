using Robust.Shared.GameStates;

namespace Content.Shared._Rat.Overwatch;

/// <summary>
/// Компонент для игрока, который смотрит через камеру другого игрока через консоль Overwatch. - Component for viewing through worn cameras via overwatch.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RatOverwatchWatchingComponent : Component
{
    /// <summary>
    /// Сущность, за которой сейчас наблюдает игрок. - Currently observed entity
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Watching;

    /// <summary>
    /// Сущность консоли Overwatch, которая управляет этим наблюдением. - Console being used
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Console;
}

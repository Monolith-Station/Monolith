using Robust.Shared.GameStates;

namespace Content.Shared._Rat.Overwatch;

/// <summary>
/// Компонент для сущности, на которую смотрят через консоль Overwatch. - Comp for entity being viewed by overwatch
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RatOverwatchCameraComponent : Component
{
    /// <summary>
    /// Игроки, которые сейчас смотрят через камеру этой сущности. - Entities currently watching this one
    /// </summary>
    [DataField, AutoNetworkedField]
    public HashSet<EntityUid> Watching = new();
}

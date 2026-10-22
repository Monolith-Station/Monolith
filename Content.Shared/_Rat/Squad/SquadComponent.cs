using Robust.Shared.GameStates;

namespace Content.Shared._Rat.Squad;

/// <summary>
/// Компонент для принадлежности сущности к отряду. - Component for assigning an entity to a squad.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SquadComponent : Component
{
    /// <summary>
    /// ID отряда - Squad ID
    /// </summary>
    [DataField, AutoNetworkedField]
    public int SquadId;

    /// <summary>
    /// Название отряда для отображения - Display name
    /// </summary>
    [DataField, AutoNetworkedField]
    public string SquadName = string.Empty;
}

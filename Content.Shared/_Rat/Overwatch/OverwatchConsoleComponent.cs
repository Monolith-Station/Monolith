using Content.Shared._Mono.Company;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Rat.Overwatch;

/// <summary>
/// Компонент консоли Overwatch для отслеживания членов фракции. - Comp for overwatch console
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class OverwatchConsoleComponent : Component
{
    /// <summary>
    /// Фракция, которую отслеживает эта консоль. - Faction/company tracked by this console
    /// </summary>
    [DataField, AutoNetworkedField]
    public ProtoId<CompanyPrototype> Faction = "None";

    /// <summary>
    /// Текущий фильтр по статусу. - Current filter for status
    /// </summary>
    [DataField, AutoNetworkedField]
    public OverwatchMemberStatus? StatusFilter;

    /// <summary>
    /// Текущий фильтр по отряду. - Current filter for squads
    /// </summary>
    [DataField, AutoNetworkedField]
    public int? SquadFilter;

    /// <summary>
    /// Текущий поисковый запрос. - Current search query
    /// </summary>
    [DataField, AutoNetworkedField]
    public string SearchQuery = string.Empty;
}

using Content.Shared.Actions;
using Content.Shared.Ninja.Systems;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Ninja.Components;

/// <summary>
/// Adds an action to dash, teleport to clicked position, when this item is held.
/// Cancel <see cref="CheckDashEvent"/> to prevent using it.
/// </summary>
[RegisterComponent, NetworkedComponent, Access(typeof(DashAbilitySystem)), AutoGenerateComponentState]
public sealed partial class DashAbilityComponent : Component
{
    /// <summary>
    /// The action id for dashing.
    /// </summary>
    [DataField]
    public EntProtoId<WorldTargetActionComponent> DashAction = "ActionEnergyKatanaDash";

    /// <summary>
    /// Mono - Do we want to check for holding the energy katana lol
    /// </summary>
    [DataField]
    public bool RequireItem = true;

    /// <summary>
    /// Mono - Is it a separate item from the user or is it itself the user?
    /// </summary>
    [DataField]
    public bool IsUser = false;

    [DataField, AutoNetworkedField]
    public EntityUid? DashActionEntity;
}

public sealed partial class DashEvent : WorldTargetActionEvent;

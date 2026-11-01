using Robust.Shared.GameStates;

namespace Content.Shared._Mono.DualWield;

/// <summary>
/// Marks a weapon as light enough to be used in one hand alongside another light weapon.
/// Holding two of these and activating one enters the dual-wield stance.
/// </summary>
/// <remarks>
/// Named LightWeapon rather than Light to avoid reading as a lighting component next to
/// PointLight, LightBehaviour and HandheldLight.
/// </remarks>
[RegisterComponent, NetworkedComponent]
public sealed partial class LightWeaponComponent : Component;

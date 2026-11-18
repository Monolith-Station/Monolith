namespace Content.Shared._Mono.Weapons.Ranged.Components;

/// <summary>
/// Drops the gun if it's unwielded while actively firing a burst. Serves best when a gun is meant to fly uncontrollably when unwielded instead of simply ceasing the burst.
/// </summary>
[RegisterComponent]
public sealed partial class GunDropOnUnwieldDuringBurstComponent : Component;
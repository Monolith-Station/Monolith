using Robust.Shared.Serialization;

namespace Content.Shared._DV.Weapons.Ranged;

[Serializable, NetSerializable]
public enum EnergyGunFireModeVisuals : byte
{
    State
}

[Serializable, NetSerializable]
public enum EnergyGunFireModeState : byte
{
    Disabler,
    Lethal,
    Special,
    // Monolith: readd ion mode
    ion,
    // End Monolith
    // Frontier: holoflare modes
    Cyan,
    Red,
    Yellow,
    // End Frontier
}

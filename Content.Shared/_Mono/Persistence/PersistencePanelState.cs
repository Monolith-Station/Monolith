using Content.Shared._FarHorizons.StarSystem.Prototypes;
using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared._Mono.Persistence;

[Serializable, NetSerializable]
public sealed class PersistencePanelState(PlanetPersistenceSummary[] planets, string? status) : EuiStateBase
{
    public readonly PlanetPersistenceSummary[] Planets = planets;
    public readonly string? Status = status;
}

[Serializable, NetSerializable]
public sealed record PlanetPersistenceSummary(string Id, string Name, PlanetPersistenceMode Mode, bool Loaded,
    PlanetPersistenceFlag[] Flags, bool SavesEnabled, TimeSpan? NextSaveAt, PlanetSaveSummary[] Saves);

[Serializable, NetSerializable]
public sealed record PlanetSaveSummary(string Id, int Round, int Seed, bool Final, string SavedAt, string Mode);

[Serializable, NetSerializable]
public sealed class SetPlanetSavesEnabled(string planetId, bool enabled) : EuiMessageBase
{
    public readonly string PlanetId = planetId;
    public readonly bool Enabled = enabled;
}

[Serializable, NetSerializable]
public sealed class DeletePlanetSave(string planetId, string saveId, string savedAt) : EuiMessageBase
{
    public readonly string PlanetId = planetId;
    public readonly string SaveId = saveId;
    public readonly string SavedAt = savedAt;
}

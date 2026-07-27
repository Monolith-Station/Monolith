using Content.Server._Forge.Station.Systems;
using Robust.Shared.Utility;

namespace Content.Server._Forge.Station.Components;

/// <summary>
/// Загружает карту и размещает на ней указанный grid при инициализации карты.
/// </summary>
[RegisterComponent, Access(typeof(StationMapSpawnerSystem))]
public sealed partial class StationMapSpawnerComponent : Component
{
    /// <summary>
    /// Путь к файлу с гридом, который будет загружен на карту.
    /// </summary>
    [DataField(required: true)]
    public ResPath? GridPath;

    /// <summary>
    /// Созданная карта (заполняется системой).
    /// </summary>
    [DataField]
    public EntityUid? Map;
}

using System.Numerics;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared._Rat.Overwatch;

/// <summary>
/// Данные о члене фракции для отображения в консоли Overwatch.
/// </summary>
[Serializable, NetSerializable]
public sealed record OverwatchMemberData(
    NetEntity Member,
    string Name,
    string JobTitle,
    OverwatchMemberStatus Status,
    bool HasCamera,
    int? SquadId = null,
    string SquadName = "",
    Vector2? Coordinates = null
);

/// <summary>
/// Статус члена фракции для отображения в консоли Overwatch.
/// </summary>
[Serializable, NetSerializable]
public enum OverwatchMemberStatus : byte
{
    Alive,
    Dead,
    SSD
}

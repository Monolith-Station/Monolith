using Robust.Shared.GameObjects;

namespace Content.Shared._Goobstation.EatToGrow;
/// <summary>
/// Raised directed at the eater after finishing eating the food before it is deleted.
/// </summary>
[ByRefEvent]
public readonly record struct AfterEatingEvent(EntityUid Food);
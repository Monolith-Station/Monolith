namespace Content.Server._CE.ZLevels.Gravity;

/// <summary>
/// When placed on a map entity (e.g. via zLevelsComponentOverrides), automatically ensures
/// every grid on this map has inherent gravity enabled — both at component init and when new grids appear.
/// </summary>
[RegisterComponent]
public sealed partial class CEAutoGridGravityComponent : Component
{
}

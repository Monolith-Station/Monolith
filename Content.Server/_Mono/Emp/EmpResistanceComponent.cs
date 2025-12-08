namespace Content.Server._Mono.Emp;

/// <summary>
/// Reduces EMP energy consumption to entity based on coefficient.
/// </summary>
[RegisterComponent]
public sealed partial class EmpResistanceComponent : Component
{
    [DataField]
    public float Coefficient = 1f;
}

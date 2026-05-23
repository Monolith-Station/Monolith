using Content.Shared.Shuttles.Systems;
using Robust.Shared.GameStates;

namespace Content.Shared.Shuttles.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedRadarConsoleSystem))]
public sealed partial class RadarConsoleComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite)]
    public float RangeVV
    {
        get => MaxRange;
        set => IoCManager
            .Resolve<IEntitySystemManager>()
            .GetEntitySystem<SharedRadarConsoleSystem>()
            .SetRange(Owner, value, this);
    }

    [DataField, AutoNetworkedField]
    public float MaxRange = 3072f; // Mono - 256->3072

    /// <summary>
    /// If true, the radar will be centered on the entity. If not - on the grid on which it is located.
    /// </summary>
    [DataField]
    public bool FollowEntity = false;

    // Frontier: ghost radar restrictions
    /// <summary>
    /// If true, the radar will be centered on the entity. If not - on the grid on which it is located.
    /// </summary>
    [DataField]
    public float? MaxIffRange = null;

    /// <summary>
    /// If true, the radar will not show the coordinates of objects on hover
    /// </summary>
    [DataField]
    public bool HideCoords = false;
    // End Frontier

    // <Mono>
    [DataField]
    public bool Pannable = true;

    /// <summary>
    /// Whether to still follow the console after being panned.
    /// </summary>
    [DataField]
    public bool RelativePanning = false;

    /// <summary>
    /// Whether to always face north-up.
    /// </summary>
    [DataField]
    public bool NoRotate = false;

    // supported behavior modes:
    // |  panned  | unpanned |  panned  | unpanned |   bool   |
    // | rotation | rotation |  anchor  |  anchor  | settings |
    // |----------|----------|----------|----------|----------|
    // | north    | north    | follow   | static   | 00       |
    // | north    | follow   | static   | follow   | 01       |
    // | follow   | follow   | follow   | follow   | 10       |
    // | north    | north    | follow   | follow   | 11       |

    // </Mono>
}

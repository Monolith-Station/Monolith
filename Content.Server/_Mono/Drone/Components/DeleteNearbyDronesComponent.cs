namespace Content.Server._Mono.Drone.Components;

[RegisterComponent]
public sealed partial class DeleteNearbyDronesComponent : Component
{
    /// <summary>
    /// The distance at which nearby drones will be deleted;
    /// </summary>
    [DataField]
    public float Distance = 5000f;
}
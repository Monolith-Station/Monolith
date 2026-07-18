using Robust.Shared.Components;
using Content.Shared.Research;

namespace Content.Shared._Mono.SectorCapture.Components;
[RegisterComponent, NetworkedComponent]
public sealed partial class TransmissionAntennaComponent : Component
{
    ///<summary>
    ///sets the default transmission rate of the antenna (by how much the normal, aligned antenna would transmit)
    /// </summary>
    [DataField]
    public float TransmissionRate = 0f;
    /// <summary>
    /// defines the degree of alignment the antenna has, from 0 to 360, value chosen for alignment will vary depending on position of the receiver in space (yes this will be hell)
    /// </summary>
    [DataField]
    public float AlignmentParameter = 1f;
}

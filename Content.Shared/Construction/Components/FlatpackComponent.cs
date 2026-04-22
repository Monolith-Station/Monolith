using Content.Shared.Physics;
using Content.Shared.Tools;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Physics.Dynamics;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.Construction.Components;

/// <summary>
/// This is used for an object that can instantly create a machine upon having a tool applied to it.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedFlatpackSystem))]
public sealed partial class FlatpackComponent : Component
{
    /// <summary>
    /// The tool quality that, upon used to interact with this object, will create the <see cref="Entity"/>
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public ProtoId<ToolQualityPrototype> QualityNeeded = "Pulsing";

    /// <summary>
    /// The entity that is spawned when this object is unpacked.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public EntProtoId? Entity;

    /// <summary>
    /// Sound effect played upon the object being unpacked.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public SoundSpecifier UnpackSound = new SoundPathSpecifier("/Audio/Effects/unwrap.ogg");

    /// <summary>
    /// A dictionary relating a machine board sprite state to a color used for the overlay.
    /// Kinda shitty but it gets the job done.
    /// </summary>
    [DataField]
    public Dictionary<string, Color> BoardColors = new();


    /// <summary>
    /// Collision layer assumption of the unflatpacked object. Used for checking if unpackable.
    /// </summary>
    [ViewVariables]
    [DataField("layer", customTypeSerializer: typeof(FlagSerializer<CollisionLayer>))]
    public int CollisionLayer = (int)CollisionGroup.MachineLayer;

    /// <summary>
    /// Collision mask assumption of the unflatpacked object. Used for checking if unpackable.
    /// </summary>
    [ViewVariables]
    [DataField("mask", customTypeSerializer: typeof(FlagSerializer<CollisionMask>))]
    public int CollisionMask = (int)CollisionGroup.MachineMask;
}

[Serializable, NetSerializable]
public enum FlatpackVisuals : byte
{
    Machine
}

public enum FlatpackVisualLayers : byte
{
    Overlay
}

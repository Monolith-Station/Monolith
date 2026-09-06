using Content.Server.Objectives.Systems;
using Content.Shared.Objectives;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Utility;

namespace Content.Server.Objectives.Components.Targets;

/// <summary>
/// Allows an object to become the target of a StealCollection  objection
/// </summary>
[RegisterComponent]
public sealed partial class StealTargetComponent : Component
{
    /// <summary>
    /// The theft group to which this item belongs.
    /// </summary>
    [DataField(required: true), ViewVariables(VVAccess.ReadWrite)]
    public PrototypeFlags<StealTargetGroupPrototype> StealGroup = []; // Mono - Multiple stealgroups
}

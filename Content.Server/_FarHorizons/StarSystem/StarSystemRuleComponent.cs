using Content.Shared._FarHorizons.StarSystem.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server._FarHorizons.StarSystem;

[RegisterComponent, Access(typeof(StarSystemRuleSystem))]
public sealed partial class StarSystemRuleComponent : Component
{
    [DataField(required: true)] public ProtoId<StarSystemPrototype> System;
}

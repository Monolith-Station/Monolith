using Content.Shared.Shuttles.Components;

namespace Content.Server._Mono.Detection;

[RegisterComponent]
public sealed partial class ApplyIFFFlagsToDockedShipsComponent : Component
{
    [DataField(required: true)]
    public IFFFlags Flags;
}
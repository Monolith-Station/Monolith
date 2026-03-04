using Content.Shared.Teleportation;

namespace Content.Server.Teleportation;

// Mono - rename
[RegisterComponent]
public sealed partial class ScramImplantComponent : Component
{
    // Mono
    [DataField]
    public TeleportSpecifier Specifier = new();
}

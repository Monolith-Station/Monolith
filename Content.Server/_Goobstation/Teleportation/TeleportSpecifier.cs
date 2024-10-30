using Robust.Shared.Audio;

// Mono - whole file
namespace Content.Server.Teleportation;

[DataDefinition]
public struct TeleportSpecifier(float TeleportRadius = 100f,
                                int TeleportAttempts = 20,
                                SoundSpecifier TeleportSound = new SoundPathSpecifier("/Audio/Effects/teleport_arrival.ogg"),
                                bool AvoidSpace = true);

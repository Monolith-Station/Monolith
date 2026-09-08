using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._CE.Planets.Shields;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CEShieldBeamComponent : Component
{
    [DataField, AutoNetworkedField]
    public NetEntity Generator;

    [DataField, AutoNetworkedField]
    public float Radius = 0.45f;

    [DataField, AutoNetworkedField]
    public bool Source;

    [DataField, AutoNetworkedField]
    public string Overlay = "Planetary";
}

[ByRefEvent]
public record struct CEShieldBeamVaporizingEvent(EntityUid Beam, NetEntity Generator)
{
    public bool Handled;
}

[Serializable, NetSerializable]
public sealed class CEShieldBeamVaporizedEvent(NetEntity victim) : EntityEventArgs
{
    public readonly NetEntity Victim = victim;
}

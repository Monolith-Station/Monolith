using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Crescent.Hardpoints;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class HardpointComponent : Component
{
    [ViewVariables(VVAccess.ReadOnly), AutoNetworkedField]
    public EntityUid? anchoring;

    [ViewVariables(VVAccess.ReadWrite), DataField("class"), AutoNetworkedField]
    public weaponTypes CompatibleTypes = weaponTypes.Ballistic;

    [ViewVariables(VVAccess.ReadWrite), DataField("size"), AutoNetworkedField]
    public weaponSizes CompatibleSizes = weaponSizes.Medium;
}

[Flags, Serializable, NetSerializable]
public enum weaponTypes
{
    Energy = 1 << 1,
    Ballistic = 1 << 2,
    Missile = 1 << 3,
    Hybrid = Energy | Ballistic,
    Composite = Ballistic | Missile,
    Synergy = Energy | Missile,
    Universal = Energy | Ballistic | Missile,
}

[Serializable, NetSerializable]
public enum weaponSizes
{
    Superlight = 1,
    Light = 2,
    Medium = 3,
    Heavy = 4,
    Superheavy = 5,
}

public class HardpointCannonAnchoredEvent : EntityEventArgs
{
    public EntityUid cannonUid;
    public EntityUid gridUid;
}

public class HardpointCannonDeanchoredEvent : EntityEventArgs
{
    public EntityUid CannonUid;
    public EntityUid gridUid;
}

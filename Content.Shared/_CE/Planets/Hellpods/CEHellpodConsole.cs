using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._CE.Planets.Hellpods;

[RegisterComponent]
public sealed partial class CEHellpodConsoleComponent : Component
{
    public static readonly TimeSpan ConfirmTime = TimeSpan.FromSeconds(3);
    public static readonly TimeSpan BoardingTime = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan LaunchDelay = TimeSpan.FromSeconds(3);
    public const float MinRadius = 8f;
    public const float MaxRadius = 128f;

    [DataField]
    public ProtoId<CEHellpodTypePrototype> PodType = "CEHellpodDefault";
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CEHellpodApproachComponent : Component
{
    public static readonly TimeSpan CloudFadeIn = TimeSpan.FromSeconds(0.6);
    public static readonly TimeSpan CloudFadeOut = TimeSpan.FromSeconds(0.25);

    [AutoNetworkedField] public EntityUid Planet;
    [AutoNetworkedField] public Vector2 FormationCenter;
    [AutoNetworkedField] public Vector2 SurfacePoint;
    [AutoNetworkedField] public TimeSpan Start;
    [AutoNetworkedField] public TimeSpan End;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CEHellpodDepartureComponent : Component
{
    [AutoNetworkedField] public TimeSpan Start;
    [AutoNetworkedField] public TimeSpan End;
    [AutoNetworkedField] public CEHellpodRicochet? Ricochet;
    [AutoNetworkedField] public EntityUid Planet;
    [AutoNetworkedField] public Vector2 Entry;
}

[Serializable, NetSerializable]
public readonly record struct CEHellpodRicochet(Vector2 Exit, bool Confirmed = false);

[Serializable, NetSerializable]
public enum CEHellpodConsoleKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public enum CEHellpodHoldAction : byte
{
    Start,
    Cancel,
    Complete,
}

[Serializable, NetSerializable]
public sealed class CEHellpodConfirmMessage(Vector2 center, float radius, CEHellpodHoldAction action) : BoundUserInterfaceMessage
{
    public readonly Vector2 Center = center;
    public readonly float Radius = radius;
    public readonly CEHellpodHoldAction Action = action;
}

[Serializable, NetSerializable]
public sealed class CEHellpodConfirmRejectedMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class CEHellpodConsoleState : BoundUserInterfaceState
{
    public ProtoId<CEHellpodTypePrototype>? PodType;
    public bool SiloConnected;
    public bool CanFundVolley;
    public string? Planet;
    public bool PlanetShielded;
    public NetEntity Terrain;
    public int MinDepth;
    public int MaxDepth;
    public int GroundDepth;
    public float Limit;
    public int Occupied;
    public int Hatches;
    public TimeSpan? ConfirmAt;
    public TimeSpan? LaunchAt;
    public TimeSpan? FireAt;
    public Vector2 Center;
    public float Radius;
    public CEHellpodContact[] Contacts = [];
}

[Serializable, NetSerializable]
public readonly record struct CEHellpodContact(Vector2 Center, Vector2 Radii, Angle Rotation,
    float Depth, float HalfHeight, bool Shield);

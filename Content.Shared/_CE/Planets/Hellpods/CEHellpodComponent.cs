using System.Numerics;
using Content.Shared.Atmos;
using Robust.Shared.GameStates;

namespace Content.Shared._CE.Planets.Hellpods;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CEHellpodComponent : Component
{
    public const int FullIntegrity = 100;
    public const int ShieldIntegrityLoss = 25;
    public const float RicochetRippleStrength = 0.25f;

    public int Integrity = FullIntegrity;
    public readonly HashSet<EntityUid> CrossedShields = new();
    public GasMixture? CabinAir;

    [AutoNetworkedField]
    public bool InFlight;

    [AutoNetworkedField]
    public Vector2 OrbitalEntry;

    [AutoNetworkedField]
    public TimeSpan? OrbitalArrivalAt;

    [AutoNetworkedField]
    public CEHellpodRicochet? Ricochet;

    [DataField]
    public float DropSpeed = 9f;

    [DataField]
    public float ImpactIntensity = 18f;
}

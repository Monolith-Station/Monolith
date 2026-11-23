using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._CE.Planets.Shields;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CEPlanetShieldComponent : Component
{

    [DataField, AutoNetworkedField]
    public bool Active;

    public const float TransitionSeconds = 2.5f;

    [AutoNetworkedField]
    public TimeSpan TransitionAt;

    [AutoNetworkedField]
    public float TransitionFrom;

    public TimeSpan NextBreachAlarm;

    public float PhaseAt(TimeSpan now)
    {
        var elapsed = MathF.Max(0f, (float) (now - TransitionAt).TotalSeconds);
        return Math.Clamp(TransitionFrom + (Active ? elapsed : -elapsed) / TransitionSeconds, 0f, 1f);
    }

    public const int MaxImpacts = 24;
    public const float ImpactLifetime = 3f;

    [AutoNetworkedField]
    public List<CEPlanetShieldImpact> Impacts = new();
}

[Serializable, NetSerializable]
public readonly record struct CEPlanetShieldImpact(Vector2 Entry, TimeSpan Time, float Strength);

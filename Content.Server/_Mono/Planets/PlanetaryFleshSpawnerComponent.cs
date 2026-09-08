using Robust.Shared.Prototypes;

namespace Content.Server._Mono.Planets;

[RegisterComponent]
public sealed partial class PlanetaryFleshSpawnerComponent : Component
{
    [DataField] public TimeSpan Interval = TimeSpan.FromSeconds(15);
    [DataField] public TimeSpan Lifetime = TimeSpan.FromSeconds(45);
    [DataField] public int NearbyLimit = 3;
    [DataField] public int MapLimit = 24;
    [DataField] public float Radius = 16f;
    [DataField] public List<EntProtoId> Mobs = new()
    {
        "MobAerumnaFleshJared", "MobAerumnaFleshGolem", "MobAerumnaFleshClamp",
    };
    public TimeSpan NextSpawn;
    public readonly Dictionary<EntityUid, TimeSpan> Spawned = new();
}

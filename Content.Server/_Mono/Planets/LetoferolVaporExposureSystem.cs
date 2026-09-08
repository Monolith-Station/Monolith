using Content.Server.Atmos.EntitySystems;
using Content.Server.Body.Components;
using Content.Server.Body.Systems;
using Content.Shared.Atmos;
using Content.Shared.Chemistry.Components;
using Content.Shared.FixedPoint;

namespace Content.Server._Mono.Planets;

public sealed partial class LetoferolVaporExposureSystem : EntitySystem
{
    [Dependency] private AtmosphereSystem _atmos = default!;
    [Dependency] private BloodstreamSystem _blood = default!;
    private float _elapsed;

    public override void Update(float frameTime)
    {
        _elapsed += frameTime;
        if (_elapsed < 1f)
            return;
        _elapsed = 0f;
        var query = EntityQueryEnumerator<BloodstreamComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var blood, out var xform))
        {
            var air = _atmos.GetContainingMixture((uid, xform));
            if (air == null || air.Volume <= 0f)
                continue;
            var amount = Math.Clamp(air.GetMoles(Gas.LetoferolVapor) / air.Volume * 500f, 0f, 5f);
            if (amount < 0.01f)
                continue;
            _blood.TryAddToChemicals(uid, new Solution("NaturalLetoferol", FixedPoint2.New(amount)), blood);
        }
    }
}

using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Content.Shared.Atmos;

namespace Content.Server._CE.ZLevels.Core;

public sealed partial class CEZTransitAtmosphereSystem : EntitySystem
{
    [Dependency] private AtmosphereSystem _atmos = default!;
    [Dependency] private CESharedZLevelsSystem _zLevels = default!;

    private float _timer;

    public override void Initialize()
    {
        base.Initialize();
        UpdatesBefore.Add(typeof(AtmosphereSystem));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _timer += frameTime;
        if (_timer < _atmos.AtmosTime)
            return;

        _timer %= _atmos.AtmosTime;

        var query = EntityQueryEnumerator<CEZTransitMapComponent>();
        while (query.MoveNext(out var uid, out var transit))
            UpdateAtmosphere(uid, transit);
    }

    private void UpdateAtmosphere(EntityUid uid, CEZTransitMapComponent transit)
    {
        if (transit.LowerMap is not { } lower)
            return;

        var lowerAtmos = CompOrNull<MapAtmosphereComponent>(lower);
        var upperAtmos = transit.UpperMap is { } upper
            ? CompOrNull<MapAtmosphereComponent>(upper)
            : null;

        if (lowerAtmos == null && upperAtmos == null)
            return;

        var progress = _zLevels.GetTransitProgress(transit);
        var mixture = Interpolate(
            lowerAtmos?.Mixture ?? GasMixture.SpaceGas,
            upperAtmos?.Mixture ?? GasMixture.SpaceGas,
            progress);
        mixture.MarkImmutable();
        var space = mixture.TotalMoles < Atmospherics.GasMinMoles;

        if (TryComp<MapAtmosphereComponent>(uid, out var current))
        {
            var currentMixture = current.Mixture;
            if (current.Space == space && currentMixture.Equals(mixture))
                return;
        }

        _atmos.SetMapAtmosphere(uid, space, mixture, false);
    }

    private GasMixture Interpolate(GasMixture lower, GasMixture upper, float progress)
    {
        if (lower.Equals(upper))
            return new GasMixture(lower);

        var volume = float.Lerp(lower.Volume, upper.Volume, progress);
        var mixture = new GasMixture(volume);

        for (var i = 0; i < Atmospherics.AdjustedNumberOfGases; i++)
            mixture.SetMoles(i, float.Lerp(lower.GetMoles(i), upper.GetMoles(i), progress));

        var lowerCapacity = lower.TotalMoles < Atmospherics.GasMinMoles
            ? 0f
            : _atmos.GetHeatCapacity(new GasMixture(lower), true);
        var upperCapacity = upper.TotalMoles < Atmospherics.GasMinMoles
            ? 0f
            : _atmos.GetHeatCapacity(new GasMixture(upper), true);
        var capacity = float.Lerp(lowerCapacity, upperCapacity, progress);

        mixture.Temperature = capacity <= 0f
            ? Atmospherics.TCMB
            : float.Lerp(lower.Temperature * lowerCapacity, upper.Temperature * upperCapacity, progress) / capacity;

        return mixture;
    }
}

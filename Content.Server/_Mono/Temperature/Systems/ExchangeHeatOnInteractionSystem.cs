using Content.Server._Mono.Temperature.Components;
using Content.Server.Temperature.Components;
using Content.Server.Temperature.Systems;
using Content.Shared.Interaction.Events;

namespace Content.Server._Mono.Temperature.Systems;

/// <summary>
/// This handles heat exchange between two entities on hug/pet interaction if user happens to have ExchangeHeatOnInteractionComponent
/// </summary>
public sealed class ExchangeHeatOnInteractionSystem : EntitySystem
{
    [Dependency] private readonly TemperatureSystem _temp = default!;
    public override void Initialize()
    {
        SubscribeLocalEvent<TemperatureComponent, InteractionSuccessEvent>(OnInteraction);
    }

    private void OnInteraction(EntityUid uid, TemperatureComponent tComp, InteractionSuccessEvent args)
    {
        var exchanger = args.User;
        if (!TryComp<TemperatureComponent>(exchanger, out var tComp2))
            return;

        var t1 = tComp.CurrentTemperature;
        var t2 = tComp2.CurrentTemperature;

        var hc1 = tComp.SpecificHeat;
        var hc2 = tComp2.SpecificHeat;

        var coeff = tComp2.InteractionExchangeCoefficient;

        var tempDiff = t2 - t1;

        var tempDiffNew = tempDiff * MathF.Exp(-coeff * MathF.Pow(hc1 + hc2, 2) / (hc1 * hc2));
        var deltaDiff = tempDiff - tempDiffNew;
        var temp1New = t1 + deltaDiff * hc2 / (hc1 + hc2);
        var temp2New = t2 - deltaDiff * hc1 / (hc1 + hc2);

        _temp.ForceChangeTemperature(uid, temp1New);
        _temp.ForceChangeTemperature(exchanger, temp2New);
    }
}

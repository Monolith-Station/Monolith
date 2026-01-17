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
        if (!TryComp<TemperatureComponent>(exchanger, out var tComp2) ||
            !TryComp<ExchangeHeatOnInteractionComponent>(exchanger, out var exchangerComp))
            return;

        var t1 = tComp.CurrentTemperature;
        var t2 = tComp2.CurrentTemperature;

        // We will take delta temp from target and will transfer it to exchanger
        var delta = Math.Abs((t2 - t1) * exchangerComp.Coefficient);

        if (t1 > t2)
        {
            _temp.ForceChangeTemperature(uid, t1 + -delta);
            _temp.ForceChangeTemperature(exchanger, t2 + delta);
        }
        else
        {
            _temp.ForceChangeTemperature(uid, t1 + delta);
            _temp.ForceChangeTemperature(exchanger, t2 + -delta);
        }
    }
}

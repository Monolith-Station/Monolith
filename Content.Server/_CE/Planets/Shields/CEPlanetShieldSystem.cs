using System.Numerics;
using Content.Server.Radio;
using Content.Server.Radio.Components;
using Content.Server.Radio.EntitySystems;
using Content.Shared._FarHorizons.StarSystem;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Content.Shared._CE.Planets.Shields;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared.Radio.Components;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._CE.Planets.Shields;

public sealed partial class CEPlanetShieldSystem : EntitySystem
{
    [Dependency] private CESharedZLevelsSystem _zLevels = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private RadioSystem _radio = default!;
    [Dependency] private SharedAudioSystem _audio = default!;

    private static readonly SoundSpecifier BreachAlarm = new SoundPathSpecifier("/Audio/Machines/alarm.ogg");
    private static readonly SoundSpecifier BreachSoundbite =
        new SoundPathSpecifier("/Audio/_Mono/Effects/Alerts/psg_breach.ogg");
    private static readonly TimeSpan BreachAlarmCooldown = TimeSpan.FromSeconds(10);
    private (EntityUid Sender, EntityUid Stack, Filter Listeners)? _breachDispatch;

    public override void Initialize()
    {
        SubscribeLocalEvent<RadioReceiveAttemptEvent>(OnBreachReceiveAttempt);
        SubscribeLocalEvent<ActiveRadioComponent, RadioReceiveEvent>(OnBreachReceive);
    }

    private void OnBreachReceiveAttempt(ref RadioReceiveAttemptEvent args)
    {
        if (_breachDispatch is { } dispatch && args.RadioSource == dispatch.Sender &&
            CompOrNull<CEZMapComponent>(Transform(args.RadioReceiver).MapUid)?.NetworkUid != dispatch.Stack)
            args.Cancelled = true;
    }

    private void OnBreachReceive(Entity<ActiveRadioComponent> receiver, ref RadioReceiveEvent args)
    {
        if (_breachDispatch is not { } dispatch || args.RadioSource != dispatch.Sender)
            return;

        var listener = HasComp<HeadsetComponent>(receiver) ? Transform(receiver).ParentUid : receiver.Owner;
        if (TryComp<ActorComponent>(listener, out var actor))
            dispatch.Listeners.AddPlayer(actor.PlayerSession);
    }

    public void AnnounceBreach(EntityUid planet)
    {
        var now = _timing.CurTime;
        if (!TryComp<CEPlanetShieldComponent>(planet, out var shield) || now < shield.NextBreachAlarm)
            return;

        var generators = EntityQueryEnumerator<CEShieldGeneratorComponent, TransformComponent>();
        while (generators.MoveNext(out var uid, out var generator, out var xform))
        {
            if (generator.Stage != CEShieldGeneratorStage.Active || generator.ShieldedPlanet != planet ||
                !TryComp<CEZMapComponent>(xform.MapUid, out var level))
                continue;

            shield.NextBreachAlarm = now + BreachAlarmCooldown;
            var listeners = Filter.Empty();
            var previous = _breachDispatch;
            _breachDispatch = (uid, level.NetworkUid, listeners);
            try
            {
                _radio.SendRadioMessage(uid, Loc.GetString("ce-planetary-shield-breach"), "Common", uid);
            }
            finally
            {
                _breachDispatch = previous;
            }

            _audio.PlayGlobal(BreachAlarm, listeners, true);
            _audio.PlayGlobal(BreachSoundbite, listeners, true);
            return;
        }
    }

    public bool TryGetPlanetForMap(EntityUid map, out EntityUid planet)
    {
        planet = default;
        if (!_zLevels.TryGetMapNetwork(map, out var network) ||
            !TryComp<PlanetSurfaceComponent>(network.Owner, out var surface) ||
            !HasComp<PlanetBodyComponent>(surface.Planet))
            return false;

        planet = surface.Planet;
        return true;
    }

    public bool SetShieldActive(EntityUid planet, bool active)
    {
        if (!HasComp<PlanetBodyComponent>(planet))
            return false;

        var shield = EnsureComp<CEPlanetShieldComponent>(planet);
        if (shield.Active == active)
            return true;

        var now = _timing.CurTime;
        shield.TransitionFrom = shield.PhaseAt(now);
        shield.TransitionAt = now;
        shield.Active = active;
        if (!active)
            shield.Impacts.Clear();
        Dirty(planet, shield);
        return true;
    }

    public void RecordImpact(EntityUid planet, Vector2 entry, TimeSpan time, float strength)
    {
        if (!TryComp<CEPlanetShieldComponent>(planet, out var shield) || !shield.Active)
            return;

        var now = _timing.CurTime;
        shield.Impacts.RemoveAll(impact => (now - impact.Time).TotalSeconds >= CEPlanetShieldComponent.ImpactLifetime);
        if (shield.Impacts.Count >= CEPlanetShieldComponent.MaxImpacts)
            shield.Impacts.RemoveAt(0);
        shield.Impacts.Add(new(entry, time, strength));
        Dirty(planet, shield);
    }

    public bool IsShielded(EntityUid planet)
    {
        return TryComp<CEPlanetShieldComponent>(planet, out var shield) && shield.Active;
    }
}

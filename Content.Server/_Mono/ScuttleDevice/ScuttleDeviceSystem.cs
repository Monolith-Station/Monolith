using Content.Server.AlertLevel;
using Content.Server.Audio;
using Content.Server.Cargo.Systems;
using Content.Server.Chat.Systems;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Pinpointer;
using Content.Server.Popups;
using Content.Shared._Mono.ScuttleDevice;
using Content.Shared.Audio;
using Content.Shared.Construction.Components;
using Content.Shared.Coordinates.Helpers;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Lock;
using Content.Shared.Maps;
using Content.Shared.Nuke;
using Content.Shared.Popups;
using Content.Shared.Shuttles.Systems;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Server._Mono.ScuttleDevice;

public sealed class ScuttleDeviceSystem : EntitySystem
{
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly ChatSystem _chatSystem = default!;
    [Dependency] private readonly ExplosionSystem _explosions = default!;
    [Dependency] private readonly LockSystem _lock = default!;
    [Dependency] private readonly NavMapSystem _navMap = default!;
    [Dependency] private readonly PointLightSystem _pointLight = default!;
    [Dependency] private readonly PopupSystem _popups = default!;
    [Dependency] private readonly ServerGlobalSoundSystem _sound = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedShuttleSystem _shuttles = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    /// <summary>
    ///     Time to leave between the nuke song and the nuke alarm playing.
    /// </summary>
    private TimeSpan NukeSongBuffer = TimeSpan.FromSeconds(1.5);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ScuttleDeviceComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<ScuttleDeviceComponent, ExaminedEvent>(OnExaminedEvent);
        SubscribeLocalEvent<ScuttleDeviceComponent, GetVerbsEvent<AlternativeVerb>>(OnAlternateVerb);
        SubscribeLocalEvent<ScuttleDeviceComponent, ScuttleDisarmDoAfterEvent>(OnDisarmDoAfter);
        SubscribeLocalEvent<ScuttleDeviceComponent, ScuttleArmDoAfterEvent>(OnArmDoAfter);
        SubscribeLocalEvent<ScuttleDeviceComponent, AnchorStateChangedEvent>(OnAnchorChange);
        SubscribeLocalEvent<ScuttleDeviceComponent, UnanchorAttemptEvent>(OnUnanchorAttempt);
        SubscribeLocalEvent<ScuttleDeviceComponent, PriceCalculationEvent>(OnGetPrice);
    }

    private void OnInit(Entity<ScuttleDeviceComponent> ent, ref ComponentInit args)
    {
        ent.Comp.RemainingTime = ent.Comp.Timer;
    }

    private void OnExaminedEvent(Entity<ScuttleDeviceComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.PlayedAlertSound)
            args.PushMarkup(Loc.GetString("nuke-examine-exploding"));
        else if (ent.Comp.Armed)
            args.PushMarkup(Loc.GetString("nuke-examine-armed"));
    }

    private void OnAlternateVerb(Entity<ScuttleDeviceComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!Transform(ent).Anchored || !args.CanComplexInteract)
            return;

        var user = args.User;

        if (ent.Comp.Armed)
            args.Verbs.Add(new AlternativeVerb()
            {
                Act = () => DisarmBombDoafter(ent, user),
                Text = Loc.GetString("scuttle-device-verb-disarm"),
                Priority = 10,
            });
        else if (!_lock.IsLocked(ent.Owner))
            args.Verbs.Add(new AlternativeVerb()
            {
                Act = () => ArmBombDoafter(ent, user),
                Text = Loc.GetString("scuttle-device-verb-arm"),
                Priority = 10,
            });
    }

    private void OnDisarmDoAfter(Entity<ScuttleDeviceComponent> ent, ref ScuttleDisarmDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        DisarmBomb((ent, ent.Comp));

        args.Handled = true;
    }

    private void OnArmDoAfter(Entity<ScuttleDeviceComponent> ent, ref ScuttleArmDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        ArmBomb((ent, ent.Comp));

        args.Handled = true;
    }

    private void OnAnchorChange(Entity<ScuttleDeviceComponent> ent, ref AnchorStateChangedEvent args)
    {
        if (ent.Comp.DisarmOnUnanchor && !args.Anchored)
            DisarmBomb((ent, ent.Comp));
    }

    private void OnUnanchorAttempt(Entity<ScuttleDeviceComponent> ent, ref UnanchorAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (ent.Comp.Armed)
            args.Cancel();
    }

    private void OnGetPrice(Entity<ScuttleDeviceComponent> ent, ref PriceCalculationEvent args)
    {
        if (!_lock.IsLocked(ent.Owner))
            args.Price += ent.Comp.UnlockedPrice;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ScuttleDeviceComponent>();
        while (query.MoveNext(out var uid, out var nuke))
        {
            if (nuke.Armed)
                TickTimer((uid, nuke), frameTime);
            else if (nuke.CooldownTime != null)
                TickCooldown((uid, nuke), frameTime);
        }
    }

    private void TickCooldown(Entity<ScuttleDeviceComponent?> ent, float frameTime)
    {
        if (!Resolve(ent, ref ent.Comp))
            return;

        ent.Comp.CooldownTime -= TimeSpan.FromSeconds(frameTime);
        if (ent.Comp.CooldownTime <= TimeSpan.FromSeconds(0))
        {
            // reset nuke to default state
            ent.Comp.CooldownTime = TimeSpan.FromSeconds(0);
        }
    }

    private void TickTimer(Entity<ScuttleDeviceComponent?> ent, float frameTime)
    {
        if (!Resolve(ent, ref ent.Comp))
            return;

        ent.Comp.RemainingTime -= TimeSpan.FromSeconds(frameTime);

        // disarm if we changed map and this should disarm us
        if (ent.Comp.DisarmOnMapChange && ent.Comp.ArmedMap != Transform(ent).MapID)
        {
            DisarmBomb(ent);
            return;
        }

        // Start playing the ent.Comp event song so that it ends a couple seconds before the alert sound
        // should play
        if (ent.Comp.DoMusic && ent.Comp.RemainingTime <= ent.Comp.NukeSongLength + ent.Comp.AlertSoundTime + NukeSongBuffer && !ent.Comp.PlayedNukeSong && !ResolvedSoundSpecifier.IsNullOrEmpty(ent.Comp.SelectedNukeSong))
        {
            _sound.DispatchStationEventMusic(ent, ent.Comp.SelectedNukeSong, StationEventMusicType.Nuke);
            ent.Comp.PlayedNukeSong = true;
        }

        // play alert sound if time is running out
        if (ent.Comp.RemainingTime <= ent.Comp.AlertSoundTime && !ent.Comp.PlayedAlertSound)
        {
            _sound.PlayGlobalOnStation(ent, _audio.ResolveSound(ent.Comp.AlertSound), new AudioParams{Volume = -5f});
            _sound.StopStationEventMusic(ent, StationEventMusicType.Nuke);
            ent.Comp.PlayedAlertSound = true;
        }

        if (ent.Comp.RemainingTime <= TimeSpan.FromSeconds(0))
        {
            ent.Comp.RemainingTime = TimeSpan.FromSeconds(0);
            ActivateBomb(ent);
        }
    }

    /// <summary>
    ///     Force a nuclear bomb to start a countdown timer
    /// </summary>
    public void ArmBomb(Entity<ScuttleDeviceComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp))
            return;

        if (ent.Comp.Armed)
            return;

        var nukeXform = Transform(ent);
        var grid = nukeXform.GridUid;
        var name = grid == null ? "Space" : _shuttles.GetIFFLabel(grid.Value) ?? "Space";

        // warn a crew
        var announcement = Loc.GetString("scuttle-device-announcement-armed",
            ("time", (int) ent.Comp.RemainingTime.TotalSeconds),
            ("location", name));
        var sender = Loc.GetString(ent.Comp.AnnounceSender);
        _chatSystem.DispatchFilteredAnnouncement(Filter.Local().AddInRange(_transform.GetMapCoordinates(ent, nukeXform), ent.Comp.AnnounceRadius),
                                                announcement, sender: sender, playSound: false, colorOverride: Color.Red);

        _sound.PlayGlobalOnStation(ent, _audio.ResolveSound(ent.Comp.ActivateSound));
        _sound.PlayGlobalOnStation(ent, _audio.ResolveSound(ent.Comp.ArmSound));
        if (ent.Comp.DoMusic)
        {
            ent.Comp.SelectedNukeSong = _audio.ResolveSound(ent.Comp.ArmMusic);
            ent.Comp.NukeSongLength = _audio.GetAudioLength(ent.Comp.SelectedNukeSong);
        }

        // turn on the spinny light
        _pointLight.SetEnabled(ent, true);
        // enable the navmap beacon for people to find it
        _navMap.SetBeaconEnabled(ent, true);

        if (!nukeXform.Anchored)
        {
            // Admin command shenanigans, just make sure.
            _transform.AnchorEntity(ent, nukeXform);
        }

        ent.Comp.ArmedMap = nukeXform.MapID;

        ent.Comp.Armed = true;
        UpdateAppearance((ent, ent.Comp));
    }

    /// <summary>
    ///     Stop nuclear bomb timer
    /// </summary>
    public void DisarmBomb(Entity<ScuttleDeviceComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp))
            return;

        if (!ent.Comp.Armed)
            return;

        var nukeXform = Transform(ent);
        var grid = nukeXform.GridUid;
        var name = grid == null ? "Space" : _shuttles.GetIFFLabel(grid.Value) ?? "Space";

        var announcement = Loc.GetString("scuttle-device-announcement-unarmed",
            ("location", name));
        var sender = Loc.GetString("scuttle-device-announcement-sender");
        _chatSystem.DispatchFilteredAnnouncement(Filter.Local().AddInRange(_transform.GetMapCoordinates(ent, nukeXform), ent.Comp.AnnounceRadius),
                                                announcement, sender: sender, playSound: false);

        ent.Comp.PlayedNukeSong = false;
        _sound.PlayGlobalOnStation(ent, _audio.ResolveSound(ent.Comp.DisarmSound));
        _sound.StopStationEventMusic(ent, StationEventMusicType.Nuke);

        // reset nuke remaining time to either itself or the minimum time, whichever is higher
        ent.Comp.RemainingTime = TimeSpan.FromSeconds(Math.Max(ent.Comp.RemainingTime.TotalSeconds, ent.Comp.MinimumTime.TotalSeconds));

        // disable sound and reset it
        ent.Comp.PlayedAlertSound = false;
        ent.Comp.AlertAudioStream = _audio.Stop(ent.Comp.AlertAudioStream);

        // turn off the spinny light
        _pointLight.SetEnabled(ent, false);
        // disable the navmap beacon now that its disarmed
        _navMap.SetBeaconEnabled(ent, false);

        // start bomb cooldown
        ent.Comp.CooldownTime = ent.Comp.Cooldown;

        ent.Comp.Armed = false;
        UpdateAppearance((ent, ent.Comp));
    }

    /// <summary>
    ///     Force bomb to explode immediately
    /// </summary>
    public void ActivateBomb(Entity<ScuttleDeviceComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp))
            return;

        _explosions.TriggerExplosive(ent);
        _sound.StopStationEventMusic(ent, StationEventMusicType.Nuke);
    }

    private void ArmBombDoafter(Entity<ScuttleDeviceComponent> ent, EntityUid user)
    {
        var doAfter = new DoAfterArgs(EntityManager, user, ent.Comp.ArmDoafterLength, new ScuttleArmDoAfterEvent(), ent, target: ent)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            NeedHand = false,
            MultiplyDelay = false,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return;

        _popups.PopupEntity(Loc.GetString("scuttle-device-arm-warning"), user,
            user, PopupType.LargeCaution);
    }

    private void DisarmBombDoafter(Entity<ScuttleDeviceComponent> ent, EntityUid user)
    {
        var doAfter = new DoAfterArgs(EntityManager, user, ent.Comp.DisarmDoafterLength, new ScuttleDisarmDoAfterEvent(), ent, target: ent)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            NeedHand = false,
            MultiplyDelay = false,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return;

        _popups.PopupEntity(Loc.GetString("scuttle-device-disarm-warning"), user,
            user, PopupType.LargeCaution);
    }

    private void UpdateAppearance(Entity<ScuttleDeviceComponent> ent)
    {
        var xform = Transform(ent);

        _appearance.SetData(ent, NukeVisuals.Deployed, xform.Anchored);

        NukeVisualState state;
        if (ent.Comp.PlayedAlertSound)
            state = NukeVisualState.YoureFucked;
        else if (ent.Comp.Armed)
            state = NukeVisualState.Armed;
        else
            state = NukeVisualState.Idle;

        _appearance.SetData(ent, NukeVisuals.State, state);
    }
}

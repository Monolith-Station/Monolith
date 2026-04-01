using Content.Server._Mono.Containment.Components;
using Content.Server.Power.EntitySystems;
using Content.Server.Research.Systems;
using Content.Shared.DeviceLinking.Events;
using Content.Shared.Examine;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Research.Components;
using Robust.Shared.Audio.Systems;

namespace Content.Server._Mono.Containment;
public sealed partial class ContainmentSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly ResearchSystem _research = default!;
    [Dependency] private readonly MobThresholdSystem _threshold = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    private TimeSpan _updateTimer =  TimeSpan.Zero;
    private float _updateCooldown = 1f;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ContainmentComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<ContainmentComponent, SignalReceivedEvent>(OnSignal);
    }

    private void OnSignal(Entity<ContainmentComponent> ent, ref SignalReceivedEvent args)
    {
        RegisterEntities(Transform(ent), ent);
    }

    private void OnExamine(EntityUid uid, ContainmentComponent component, ExaminedEvent args)
    {
        foreach (var ent in component.ActiveEntities)
        {
            if (ent == null || !TryComp<ContainableEntityComponent>(ent, out var cont))
                continue;

            var output = GetPointOutput(ent.Value, cont, component);
            if (output <= 0)
                continue;

            var meta = MetaData(ent.Value);
            args.PushMarkup(Loc.GetString("containment-examine-verb",
                ("entity_name", meta.EntityName),
                ("points", output)));
        }
    }

    public override void Update(float frameTime)
    {
        if (_updateTimer < TimeSpan.FromSeconds(_updateCooldown))
        {
            _updateTimer += TimeSpan.FromSeconds(frameTime);
            return;
        }

        var containments = EntityQueryEnumerator<ContainmentComponent>();

        while (containments.MoveNext(out var uid, out var containment))
        {
            foreach (var ent in _entityRemoveQueue)
            {
                if (containment.ActiveEntities.Contains(ent))
                    containment.ActiveEntities.Remove(ent);
            }

            if (!this.IsPowered(uid, EntityManager))
                continue;

            var xform = Transform(uid);
            UpdateEntity(xform, (uid, containment));
        }

        _updateTimer -= TimeSpan.FromSeconds(_updateCooldown);
    }

    public void AddPoints(float points, EntityUid uid)
    {
        if (!TryComp<ResearchClientComponent>(uid, out var client) || !client.Server.HasValue)
            return;

        _research.ModifyServerPoints(client.Server.Value, (int) points);
    }
}

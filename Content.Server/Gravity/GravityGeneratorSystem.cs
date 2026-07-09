using Content.Server._CE.ZLevels.Core; // pzn: gravgen load readout
using Content.Server.Emp; // Frontier: Upstream - #28984
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared.Examine; // pzn: mass limit examine
using Content.Shared.Gravity;

namespace Content.Server.Gravity;

public sealed partial class GravityGeneratorSystem : EntitySystem
{
    [Dependency] private GravitySystem _gravitySystem = default!;
    [Dependency] private SharedPointLightSystem _lights = default!;
    [Dependency] private CEZLevelsSystem _zLevels = default!; // pzn: gravgen load readout

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GravityGeneratorComponent, EntParentChangedMessage>(OnParentChanged);
        SubscribeLocalEvent<GravityGeneratorComponent, ChargedMachineActivatedEvent>(OnActivated);
        SubscribeLocalEvent<GravityGeneratorComponent, ChargedMachineDeactivatedEvent>(OnDeactivated);
        // SubscribeLocalEvent<GravityGeneratorComponent, EmpPulseEvent>(OnEmpPulse); // Frontier: Upstream - #28984
        SubscribeLocalEvent<GravityGeneratorComponent, ExaminedEvent>(OnExamined); // pzn: mass limit
    }

    // pzn: state the rated mass capacity so people know why their brick fell out of the sky
    private void OnExamined(Entity<GravityGeneratorComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.MaxHandledMass == 0f)
            return;

        if (ent.Comp.MaxHandledMass > 0f)
        {
            // raw physics mass ("kilograms"). Yes, a tile is 0.5 of these.
            // Players can do the math themselves. Fuck you guys.
            args.PushMarkup(Loc.GetString("gravity-generator-examine-max-mass",
                ("mass", ent.Comp.MaxHandledMass)));
        }

        // will it lift
        if (!TryGetLoad(ent, out var mass, out var capacity))
            return;

        if (float.IsPositiveInfinity(capacity))
        {
            args.PushMarkup(Loc.GetString("gravity-generator-examine-load-unlimited"));
            return;
        }

        var percent = mass / capacity * 100f;
        args.PushMarkup(Loc.GetString("gravity-generator-examine-load",
            ("percent", MathF.Round(percent)),
            ("color", LoadColor(percent))));
    }

    /// <summary>
    /// pzn: pooled load for the grid this gravgen sits on. An idle generator still
    /// counts its own rating, so you can read what it *would* carry once spun up.
    /// </summary>
    private bool TryGetLoad(Entity<GravityGeneratorComponent> ent, out float mass, out float capacity)
    {
        var gridUid = Transform(ent).ParentUid;
        if (!_zLevels.TryGetGravgenLoad(gridUid, out mass, out capacity))
            return false;

        if (!ent.Comp.GravityActive)
            capacity += ent.Comp.MaxHandledMass < 0f ? float.PositiveInfinity : ent.Comp.MaxHandledMass;

        return capacity > 0f;
    }

    private static string LoadColor(float percent)
    {
        var t = Math.Clamp(percent / 100f, 0f, 1f);
        var color = t < 0.5f
            ? Color.InterpolateBetween(Color.FromHex("#3fb54a"), Color.FromHex("#e6d227"), t * 2f)
            : Color.InterpolateBetween(Color.FromHex("#e6d227"), Color.FromHex("#d43d3d"), (t - 0.5f) * 2f);
        return color.ToHex();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var query = EntityQueryEnumerator<GravityGeneratorComponent, PowerChargeComponent>();
        while (query.MoveNext(out var uid, out var grav, out var charge))
        {
            if (!_lights.TryGetLight(uid, out var pointLight))
                continue;

            _lights.SetEnabled(uid, charge.Charge > 0, pointLight);
            _lights.SetRadius(uid, MathHelper.Lerp(grav.LightRadiusMin, grav.LightRadiusMax, charge.Charge),
                pointLight);
        }
    }

    private void OnActivated(Entity<GravityGeneratorComponent> ent, ref ChargedMachineActivatedEvent args)
    {
        ent.Comp.GravityActive = true;

        var xform = Transform(ent);

        if (TryComp(xform.ParentUid, out GravityComponent? gravity))
        {
            _gravitySystem.EnableGravity(xform.ParentUid, gravity);
        }
    }

    private void OnDeactivated(Entity<GravityGeneratorComponent> ent, ref ChargedMachineDeactivatedEvent args)
    {
        ent.Comp.GravityActive = false;

        var xform = Transform(ent);

        if (TryComp(xform.ParentUid, out GravityComponent? gravity))
        {
            _gravitySystem.RefreshGravity(xform.ParentUid, gravity);
        }
    }

    private void OnParentChanged(EntityUid uid, GravityGeneratorComponent component, ref EntParentChangedMessage args)
    {
        if (component.GravityActive && TryComp(args.OldParent, out GravityComponent? gravity))
        {
            _gravitySystem.RefreshGravity(args.OldParent.Value, gravity);
        }
    }
}

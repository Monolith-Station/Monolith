using Content.Server.Power.Components;
using Content.Shared._Crescent.ShipShields;
using Content.Shared._Mono.SpaceArtillery;
using Content.Shared.Physics;
using Content.Shared.Projectiles;
using Robust.Server.GameObjects;
using Robust.Server.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Collision.Shapes;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using System.Numerics;
using Content.Server._Crescent.ShipShields.Components;


namespace Content.Server._Crescent.ShipShields;

public sealed partial class ShipShieldsSystem : EntitySystem
{
    private const string ShipShieldPrototype = "ShipShield";

    //private const float DeflectionSpread = 25f;
    private const float EmitterUpdateRate = 1.5f;

    [Dependency] private SharedTransformSystem _transformSystem = default!;
    [Dependency] private FixtureSystem _fixtureSystem = default!;
    [Dependency] private PhysicsSystem _physicsSystem = default!;
    [Dependency] private PvsOverrideSystem _pvsSys = default!;

    private EntityQuery<ProjectileComponent> _projectileQuery;
    private EntityQuery<ShipWeaponProjectileComponent> _shipWeaponProjectileQuery;
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ShipShieldEmitterComponent, ApcPowerReceiverComponent>();
        while (query.MoveNext(out var uid, out var emitter, out var power))
        {
            emitter.Accumulator += frameTime;

            if (emitter.Accumulator < EmitterUpdateRate)
                continue;

            if (CalculateLoadDamage(emitter) >= emitter.MaxDraw)
                emitter.Recharging = true;
            if (!power.Powered)
                emitter.Recharging = true;

            emitter.Accumulator -= EmitterUpdateRate;
            if (emitter.OverloadAccumulator > 0)
            {
                emitter.OverloadAccumulator -= EmitterUpdateRate;
            }

            float healed = emitter.HealPerSecond * EmitterUpdateRate;

            if (emitter.Recharging)
                healed *= emitter.UnpoweredBonus;

            emitter.Damage -= healed;

            if (emitter.Damage < 0)
            {
                emitter.Damage = 0;
                if (power.Powered)
                    emitter.Recharging = false;
            }

            AdjustEmitterLoad(uid, emitter, power);

            var parent = Transform(uid).GridUid;

            if (parent == null)
                continue;

            var filter = _station.GetInOwningStation(uid);
        // Mono start
        var damageCoefficient = Math.Clamp(1 - (emitter.Damage / emitter.DamageLimit), 0, 1); // Mono - HPCoefficient based on damage overload shieldbreak condition
        var loadDamageCoefficient = Math.Clamp(1 - (CalculateLoadDamage(emitter) / emitter.MaxDraw), 0, 1); //HPCoefficient based on power load damage overload shieldbreak condition
        var HPCoefficient = damageCoefficient;
        if(loadDamageCoefficient < damageCoefficient)
        HPCoefficient = loadDamageCoefficient;

        if(TryComp<ShipShieldVisualsComponent>(emitter.Shield, out var visuals)){
        visuals.ShieldColor = emitter.ShieldColor.WithAlpha(HPCoefficient);
        Dirty(emitter.Shield.Value, visuals); //pass it to client HERE so it properly updates on damage taken
        }
        // Mono end
            if (emitter.Damage > emitter.DamageLimit)
                emitter.OverloadAccumulator = emitter.DamageOverloadTimePunishment;

            if (!emitter.Recharging && emitter.Shield is null && emitter.OverloadAccumulator < 1)
            {
                var shield = ShieldEntity(parent.Value, uid);
                if (shield != EntityUid.Invalid)
                {
                    emitter.Shield = shield;
                    emitter.Shielded = parent.Value;
                }
                _audio.PlayGlobal(emitter.PowerUpSound, filter, true, emitter.PowerUpSound.Params);
            }
            else if ((emitter.Recharging || emitter.OverloadAccumulator > 0) && emitter.Shield is not null || HasComp<ShipShieldDisabledGridComponent>(Transform(uid).GridUid))
            {
                UnshieldEntity(parent.Value);
                emitter.Shield = null;
                emitter.Shielded = null;
                if (!HasComp<ShipShieldDisabledGridComponent>(Transform(uid).GridUid))
                    _audio.PlayGlobal(emitter.PowerDownSound, filter, true, emitter.PowerUpSound.Params);
            }
        }
    }
    public override void Initialize()
    {
        base.Initialize();
        _projectileQuery = GetEntityQuery<ProjectileComponent>();
        _shipWeaponProjectileQuery = GetEntityQuery<ShipWeaponProjectileComponent>();

        SubscribeLocalEvent<ShipShieldComponent, PreventCollideEvent>(OnPreventCollide);
        SubscribeLocalEvent<ShipShieldEmitterComponent, ComponentShutdown>(OnEmitterShutdown); // Mono

        InitializeCommands();
        InitializeEmitters();
    }

    private void OnPreventCollide(EntityUid uid, ShipShieldComponent component, ref PreventCollideEvent args)
    {
        // only handle ship weapons for now. engine update introduced physics regressions. Let's polish everything else and circle back yeah?
        // Ensuring projectiles coming froms same grid don't hit shield is handled by ProjectileGridPhaseComponent
        if (!_shipWeaponProjectileQuery.HasComponent(args.OtherEntity) ||
        !_projectileQuery.TryGetComponent(args.OtherEntity, out var projectile) ||
        projectile.ProjectileSpent)
        {
            args.Cancelled = true;
            return;
        }

        //if (TryComp<TimedDespawnComponent>(args.OtherEntity, out var despawn))
        //    despawn.Lifetime += despawn.Lifetime;

        // I originally tried reflection but the math is too hard with the fucked coordinate system in this game (WorldRotation can be negative. Vector to Angle conversion loses information. Etc etc.)
        // Might try again at some point using just vector math with this (https://math.stackexchange.com/questions/13261/how-to-get-a-reflection-vector)
        //var deflectionVector = Transform(args.OtherEntity).WorldPosition - Transform(uid).WorldPosition;
        //var angle = _random.NextFloat(DeflectionSpread);

        //if (_random.Prob(0.5f))
        //    angle = -angle;

        //deflectionVector = new Vector2((float) (Math.Cos(angle) * deflectionVector.X - Math.Sin(angle) * deflectionVector.Y), (float) (Math.Sin(angle) * deflectionVector.X - Math.Cos(angle) * deflectionVector.Y));

        // instead of reflecting the projectile, just delete it. this works better for gameplay and intuiting what is going on in a fight.
        // why shoot the projectile again when you can just 180 its physics, tho?
        //_gun.ShootProjectile(args.OtherEntity, deflectionVector, _physicsSystem.GetMapLinearVelocity(uid), uid, null, velocity.Length());

        if (component.Source is { } source)
        {
            var ev = new ShieldDeflectedEvent(args.OtherEntity, projectile);
            RaiseLocalEvent(source, ref ev);
        }
    }

    private void OnEmitterShutdown(EntityUid uid, ShipShieldEmitterComponent emitter, ComponentShutdown args) // Mono
    {
        if (emitter.Shielded != null)
        {
            UnshieldEntity(emitter.Shielded.Value);
            emitter.Shield = null;
            emitter.Shielded = null;
        }
    }

    /// <summary>
    /// Produces a shield around a grid entity, if it doesn't already exist.
    /// </summary>
    /// <param name="entity">The entity being shielded.</param>
    /// <param name="mapGrid">The map grid component of the entity being shielded.</param>
    /// <param name="source">A shield generator or similar providing the shield for the entity</param>
    /// <returns>The shield entity.</returns>
    private EntityUid ShieldEntity(EntityUid entity, EntityUid? source = null, MapGridComponent? mapGrid = null)
    {
        if (TryComp<ShipShieldedComponent>(entity, out var existingShielded))
            return existingShielded.Shield;

        if (!Resolve(entity, ref mapGrid, false) || HasComp<ShipShieldDisabledGridComponent>(Transform(entity).GridUid))
            return EntityUid.Invalid;

        var prototype = ShipShieldPrototype;

        var shield = Spawn(prototype, Transform(entity).Coordinates);
        var shieldPhysics = EnsureComp<PhysicsComponent>(shield);
        var shieldComp = EnsureComp<ShipShieldComponent>(shield);
        shieldComp.Shielded = entity;
        shieldComp.Source = source;

        // Copy shield color from the generator to the shield visuals
        // AND shield arc + shield Direction -- Mono
        var shieldVisuals = EnsureComp<ShipShieldVisualsComponent>(shield);
        if (source != null && TryComp<ShipShieldEmitterComponent>(source.Value, out var emitter))
        {
            shieldVisuals.ShieldColor = emitter.ShieldColor;
            shieldVisuals.ShieldArc = emitter.ShieldArc; // Mono
            shieldVisuals.ShieldDirection = emitter.ShieldDirection; // Mono
            Dirty(shield, shieldVisuals);
        }

        var gridCenter = new EntityCoordinates(entity, mapGrid.LocalAABB.Center);
        _transformSystem.SetCoordinates(shield, gridCenter);
        _transformSystem.SetWorldRotation(shield, _transformSystem.GetWorldRotation(entity));

        var chain = GenerateOvalFixture(shield, "shield", shieldPhysics, mapGrid, shieldVisuals.Padding, shieldVisuals.ShieldDirection, shieldVisuals.ShieldArc);

        List<Vector2> roughPoly = new();
        var internalPoly = new PolygonShape();
        var secondaryInternalPoly = new PolygonShape();
        List<Vector2> secondRoughPoly = new();
        var offsetZero = Vector2.Zero; //the shield center vertex we will offset by height/width of the grid towards the direction of the shield front to make for fairer hitboxes
        float midPoint = MathF.PI / 2f - MathHelper.DegreesToRadians(shieldVisuals.ShieldDirection); //taken from createArc


        offsetZero = new Vector2(MathF.Cos(midPoint), MathF.Sin(midPoint));
        if(mapGrid.LocalAABB.Height < mapGrid.LocalAABB.Width){ //oval shield scale X
            Log.Debug($"OFFSET X: {offsetZero.X}");
            offsetZero *= (mapGrid.LocalAABB.Width / 2);
            Log.Debug($"MULT OFFSET X: {offsetZero.X}");

        }
        else{ //oval shield scale Y
            Log.Debug($"OFFSET Y: {offsetZero.Y}");
            offsetZero *= (mapGrid.LocalAABB.Height / 2);
            Log.Debug($"MULT OFFSET Y: {offsetZero.X}");
        }

        if(shieldVisuals.ShieldArc <= 180 || shieldVisuals.ShieldArc == 360){ //for single polygon shields
            roughPoly.Add(Vector2.Zero); //add the centrepoint of the ship because otherwise collision is a bit fucked for thin arcs, slightly reduces poly count for circle shields but i think its fine
            for(var i = 0; i < PhysicsConstants.MaxPolygonVertices - 1; i++){ //since we can only have X points in our polygon we have to spread them out evenly across our vertex chain for the hitbox
                var vertIndex = (int) MathF.Round(i * (chain.Count - 1) / (float) (PhysicsConstants.MaxPolygonVertices - 2)); //this makes sure we will ALWAYS get the last vertex, and the first one at index 0 as well
                roughPoly.Add(chain.Vertices[vertIndex]);
            }
            // Mono end

            internalPoly.Set(roughPoly);
        }
        else{ //for >180 degree shield arcs we use TWO polygons so we make sure the shield arc doesnt lie about its hitbox
            roughPoly.Add(offsetZero); //offset centrepoint to make hitboxes more consistent
            secondRoughPoly.Add(offsetZero); //offset centrepoint to make hitboxes more consistent
            var vertIndex = 0;
            // Log.Debug($"[3] MAXVERTINDEX: {chain.Vertices.Length}");
            for(var i = 0; i < PhysicsConstants.MaxPolygonVertices - 1; i++){ //POLYGON ONE
                vertIndex = (int) MathF.Round(i * ((chain.Count / 2) - 1) / (float) (PhysicsConstants.MaxPolygonVertices - 2)); //this makes sure we will ALWAYS get the last vertex, and the first one at index 0 as well
                roughPoly.Add(chain.Vertices[vertIndex]);
                // Log.Debug($"[1] VERTINDEX: {vertIndex}");

                vertIndex = (int) MathF.Round((i + (PhysicsConstants.MaxPolygonVertices - 2)) * ((chain.Count / 2) - 1) / (float) (PhysicsConstants.MaxPolygonVertices - 2));
                // Log.Debug($"[2] VERTINDEX: {vertIndex}");
                secondRoughPoly.Add(chain.Vertices[vertIndex]);
            }
            secondaryInternalPoly.Set(secondRoughPoly);

            _fixtureSystem.TryCreateFixture(shield, secondaryInternalPoly, "secondaryInternalShield",
            hard: true,
            collisionLayer: (int)CollisionGroup.BulletImpassable + (int)CollisionGroup.LaserImpassable, // Mono - Only try to block bullets AND LASERS
            body: shieldPhysics);

            }

            internalPoly.Set(roughPoly);

        _fixtureSystem.TryCreateFixture(shield, internalPoly, "internalShield",
            hard: true,
            collisionLayer: (int)CollisionGroup.BulletImpassable + (int)CollisionGroup.LaserImpassable, // Mono - Only try to block bullets AND LASERS
            body: shieldPhysics);

        _physicsSystem.WakeBody(shield, body: shieldPhysics);
        _physicsSystem.SetSleepingAllowed(shield, shieldPhysics, false);

        _pvsSys.AddGlobalOverride(shield);

        var shieldedComp = EnsureComp<ShipShieldedComponent>(entity);
        shieldedComp.Shield = shield;
        shieldedComp.Source = source;

        return shield;
    }

    private bool UnshieldEntity(EntityUid uid, ShipShieldedComponent? component = null)
    {
        if (!Resolve(uid, ref component, false))
            return false;

        TryQueueDel(component.Shield);
        RemComp<ShipShieldedComponent>(uid);
        return true;
    }

    private ChainShape GenerateOvalFixture(EntityUid uid, string name, PhysicsComponent physics, MapGridComponent mapGrid, float padding, float shieldDir, float shieldArc)
    {
        float radius;
        float scale;
        var scaleX = true;

        var height = mapGrid.LocalAABB.Height + padding;
        var width = mapGrid.LocalAABB.Width + padding;

        if (width > height)
        {
            radius = 0.5f * height;
            scale = width / height;
        }
        else
        {
            radius = 0.5f * width;
            scale = height / width;
            scaleX = false;
        }

        var chain = new ChainShape();
        // Mono start, made a separate case for arc shields
        if(shieldArc < 360f){ //Mono, use arc drawing function for noncircles, and circle func for circles, we do not want shield arcs between 180 and 360 as those cause unintended behaviour
            CreateArc(chain, radius, shieldDir, shieldArc);
                if(scaleX){ // because we gave it "ghost vertices" when drawing it so we have to scale those as well
                chain.NextVertex.X *= scale;
                chain.PrevVertex.X *= scale;
                }
                else{
                chain.NextVertex.Y *= scale;
                chain.PrevVertex.Y *= scale;
                }
        }
        else{
            chain.CreateLoop(Vector2.Zero, radius);
        } // Mono end
        for (int i = 0; i < chain.Vertices.Length; i++)
        {
            if (scaleX)
            {
                chain.Vertices[i].X *= scale;
            }
            else
            {
                chain.Vertices[i].Y *= scale;
            }
        }

        _fixtureSystem.TryCreateFixture(uid, chain, name,
            hard: false,
            collisionLayer: (int)CollisionGroup.BulletImpassable, // Mono - Only blocks bullets
            body: physics);

        return chain;
    }

    [ByRefEvent]
    public record struct ShieldDeflectedEvent(EntityUid Deflected, ProjectileComponent Projectile)
    {

    }
    /// Mono
    /// <summary>
    /// Function to draw an arc, based heavily on the createLoop function from RT, had to make a new one
    /// to add functionality for arcs for directional shields
    /// </summary>
    private static void CreateArc(ChainShape chain, float radius, float direction, float arcWidth, float count = 16f)
    {

        int divisions = Math.Max(4,(int)(radius * count * arcWidth / 360f)); //the number of chains we create to make the proper arc
        arcWidth = MathHelper.DegreesToRadians(arcWidth); //convert here because we need it in radians for the startPos calculation
        float stepWidth = arcWidth / divisions; // the width we give per chain we generate

        float midPoint = MathF.PI / 2f - MathHelper.DegreesToRadians(direction);
        float startPoint = midPoint - arcWidth / 2;

        Span<Vector2> vertices = stackalloc Vector2[divisions + 1]; //because we start from 0 degrees so we end it at n+1 to account for the offset

        for (int i = 0; i <= divisions; i++)
        {
            float startAngle = startPoint + stepWidth * i;
            var startPos = new Vector2(MathF.Cos(startAngle) * radius, MathF.Sin(startAngle) * radius);
            vertices[i] = startPos;
        }

        var v1 = new Vector2(MathF.Cos(startPoint - stepWidth) * radius, MathF.Sin(startPoint - stepWidth) * radius); // "beginning" vertex that is 1 step before the first one in vertices
        var v2 = new Vector2(MathF.Cos(startPoint + arcWidth + stepWidth) * radius, MathF.Sin(startPoint + arcWidth + stepWidth) * radius); // "end" vertex that is 1 step after the last one in vertices
        chain.CreateChain(vertices, v1, v2);
    }


}

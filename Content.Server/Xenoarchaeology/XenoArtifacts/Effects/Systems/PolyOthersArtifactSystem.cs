using Content.Server.Polymorph.Systems;
using Content.Server.Xenoarchaeology.XenoArtifacts.Effects.Components;
using Content.Server.Xenoarchaeology.XenoArtifacts.Events;
using Content.Shared.Humanoid;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Audio.Systems;

namespace Content.Server.Xenoarchaeology.XenoArtifacts.Effects.Systems;

public sealed partial class PolyOthersArtifactSystem : EntitySystem
{
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private MobStateSystem _mob = default!;
    [Dependency] private PolymorphSystem _poly = default!;
    [Dependency] private SharedAudioSystem _audio = default!;

    /// <summary>
    /// On effect trigger polymorphs targets in range.
    /// </summary>
    public override void Initialize()
    {
        SubscribeLocalEvent<PolyOthersArtifactComponent, ArtifactActivatedEvent>(OnActivate);
    }

    /// <summary>
    /// Provided target is alive and is not a zombie, polymorphs the target.
    /// </summary>
    private void OnActivate(Entity<PolyOthersArtifactComponent> ent, ref ArtifactActivatedEvent args)
    {
        var xform = Transform(ent);
        var humanoids = new HashSet<Entity<HumanoidAppearanceComponent>>();
        _lookup.GetEntitiesInRange(xform.Coordinates, ent.Comp.Range, humanoids);

        foreach (var comp in humanoids)
        {
            var target = comp.Owner;
            if (_mob.IsAlive(target))
            {
                _poly.PolymorphEntity(target, ent.Comp.PolymorphPrototypeName);
                _audio.PlayPvs(ent.Comp.PolySound, ent);
            }
        }
    }
}

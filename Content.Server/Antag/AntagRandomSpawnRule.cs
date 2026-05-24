using Content.Server.Antag.Components;
using Content.Server.GameTicking.Rules;

namespace Content.Server.Antag;

public sealed partial class AntagRandomSpawnSystem : GameRuleSystem<AntagRandomSpawnComponent>
{
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AntagRandomSpawnComponent, AntagSelectLocationEvent>(OnSelectLocation);
    }

    private void OnSelectLocation(Entity<AntagRandomSpawnComponent> ent, ref AntagSelectLocationEvent args)
    {
        if (TryFindRandomTile(out _, out _, out _, out var coords))
            args.Coordinates.Add(_transform.ToMapCoordinates(coords));
    }
}

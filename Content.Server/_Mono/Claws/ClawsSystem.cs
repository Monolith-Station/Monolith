using Content.Shared._Mono.Claws;

namespace Content.Server._Mono.Claws;

/// <summary>
/// This system is supposed to update claws separately from Shared system.
/// </summary>
public sealed class ClawsSystem : SharedClawsSystem
{
    private readonly float _updateCooldown = 10f;
    private TimeSpan _updateTimer = TimeSpan.Zero;

    public override void Update(float frameTime)
    {
        if (_updateTimer < TimeSpan.FromSeconds(_updateCooldown))
        {
            _updateTimer += TimeSpan.FromSeconds(frameTime);
            return;
        }

        var ents = EntityQueryEnumerator<ClawsComponent>();

        while (ents.MoveNext(out var uid, out var comp))
        {
            comp.GrowTimer += TimeSpan.FromSeconds(_updateCooldown);

            if (comp.GrowTimer < comp.GrowCooldown ||
                comp.ClawStage >= comp.Stages.Count - 1)
            {
                UpdateClaws(uid, comp); // Pretty sure we can afford that.
                Dirty(uid, comp);
                continue;
            }

            comp.GrowTimer = TimeSpan.Zero;
            comp.ClawStage += 1;

            UpdateClaws(uid, comp);
            Dirty(uid, comp);
        }

        _updateTimer = TimeSpan.Zero;

    }
}

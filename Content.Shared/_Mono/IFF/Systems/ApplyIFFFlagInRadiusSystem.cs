using Content.Shared._Mono.IFF.Components;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;
using Content.Shared.Whitelist;

namespace Content.Shared._Mono.IFF.Systems;

public sealed partial class ApplyIFFFlagInRadiusSystem : EntitySystem
{
    [Dependency] private EntityWhitelistSystem _whitelist = default!;
    [Dependency] private SharedShuttleSystem _shuttle = default!;
    [Dependency] private SharedTransformSystem _xform = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ApplyIFFFlagInRadiusComponent>();
        while (query.MoveNext(out var applyUid, out var applyComp))
        {
            var iffQuery = EntityQueryEnumerator<IFFComponent>();
            while (iffQuery.MoveNext(out var iffUid, out var iffComp))
            {
                if (_whitelist.IsBlacklistPassOrNull(applyComp.Blacklist, iffUid))
                    continue;
                if (Transform(applyUid).MapID != Transform(iffUid).MapID) // this shouldn't affect something on a different map
                    continue;

                var distance = _xform.GetWorldPosition(applyUid) - _xform.GetWorldPosition(iffUid);
                if (distance.Length() < applyComp.Radius ^ applyComp.Inverted)
                {
                    if ((iffComp.Flags & applyComp.Flags) == 0)
                        _shuttle.AddIFFFlag(iffUid, applyComp.Flags, iffComp);
                }
                else if ((iffComp.Flags & applyComp.Flags) != 0)
                    _shuttle.RemoveIFFFlag(iffUid, applyComp.Flags, iffComp);
            }
        }
    }
}
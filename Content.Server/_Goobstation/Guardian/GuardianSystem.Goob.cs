using Content.Shared.Polymorph;

namespace Content.Server.Guardian;

public sealed partial class GuardianSystem
{
    private bool _pullingGuardian;

    private void PullGuardianToHost(
        EntityUid guardianUid,
        GuardianComponent guardianComponent,
        TransformComponent hostXform,
        TransformComponent guardianXform)
    {
        if (_pullingGuardian)
            return;

        _pullingGuardian = true;
        try
        {
            if (hostXform.MapID != guardianXform.MapID)
            {
                _transform.SetCoordinates(guardianUid, guardianXform, hostXform.Coordinates);
                return;
            }

            var hostPos = _transform.WithEntityId(hostXform.Coordinates, guardianXform.ParentUid).Position;
            var diff = guardianXform.LocalPosition - hostPos;
            if (diff.LengthSquared() < 0.0001f)
            {
                _transform.SetLocalPosition(guardianUid, hostPos, guardianXform);
                return;
            }

            var newDiff = diff.Normalized() * guardianComponent.DistanceAllowed * 0.95f;
            _transform.SetLocalPosition(guardianUid, hostPos + newDiff, guardianXform);
        }
        finally
        {
            _pullingGuardian = false;
        }
    }

    private void OnHostPolymorphed(Entity<GuardianHostComponent> ent, ref PolymorphedEvent args)
    {
        if (args.NewEntity == ent.Owner
            || ent.Comp.HostedGuardian is not { } guardian
            || !TryComp<GuardianComponent>(guardian, out var guardianComp))
            return;

        var newHost = EnsureComp<GuardianHostComponent>(args.NewEntity);
        if (newHost.HostedGuardian != null && newHost.HostedGuardian != guardian)
            return;

        if (ent.Comp.GuardianContainer.Contains(guardian))
        {
            _container.Remove(guardian, ent.Comp.GuardianContainer);
            _container.Insert(guardian, newHost.GuardianContainer);
        }

        ent.Comp.HostedGuardian = null;
        newHost.HostedGuardian = guardian;
        guardianComp.Host = args.NewEntity;
    }
}

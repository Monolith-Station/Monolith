using Content.Server.Movement.Components;
using Content.Shared._Mono.Movement.Systems;

namespace Content.Server._Mono.Movement.Systems;

public sealed class FocusToggleSystem : SharedFocusToggleSystem
{
    protected override bool HasCompEyeCursorOffset(EntityUid uid)
    {
        return HasComp<EyeCursorOffsetComponent>(uid);
    }

    protected override void AddCompEyeCursorOffset(EntityUid uid)
    {
        EnsureComp<EyeCursorOffsetComponent>(uid);
    }

    protected override void RemCompEyeCursorOffset(EntityUid uid)
    {
        RemComp<EyeCursorOffsetComponent>(uid);
    }
}


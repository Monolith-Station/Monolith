using Content.Shared.Input;
using Content.Shared.Shuttles.Components;
using Robust.Shared.Input.Binding;
using Robust.Shared.Player;
using Robust.Shared.Serialization;

namespace Content.Shared._Mono.Movement.Systems;

public abstract class SharedFocusToggleSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        CommandBinds.Builder
            .Bind(ContentKeyFunctions.ToggleFocus, InputCmdHandler.FromDelegate(ToggleEyeCursorOffset))
            .Register<SharedFocusToggleSystem>();

        SubscribeNetworkEvent<ToggleEyeCursorOffsetEvent>(OnToggleEyeCursorOffset);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        CommandBinds.Unregister<SharedFocusToggleSystem>();
    }

    private void ToggleEyeCursorOffset(ICommonSession? session)
    {
        if (session?.AttachedEntity == null)
            return;

        RaiseNetworkEvent(new ToggleEyeCursorOffsetEvent());
    }

    protected virtual void OnToggleEyeCursorOffset(ToggleEyeCursorOffsetEvent ev, EntitySessionEventArgs args)
    {
        if (!args.SenderSession.AttachedEntity.HasValue)
            return;

        var uid = args.SenderSession.AttachedEntity.Value;

        if (TryComp<PilotComponent>(uid, out var pilot) && pilot.Console != null)
            return;

        if (HasCompEyeCursorOffset(uid))
            RemCompEyeCursorOffset(uid);
        else
            AddCompEyeCursorOffset(uid);
    }

    protected abstract bool HasCompEyeCursorOffset(EntityUid uid);
    protected abstract void AddCompEyeCursorOffset(EntityUid uid);
    protected abstract void RemCompEyeCursorOffset(EntityUid uid);
}

[Serializable, NetSerializable]
public sealed class ToggleEyeCursorOffsetEvent : EntityEventArgs;

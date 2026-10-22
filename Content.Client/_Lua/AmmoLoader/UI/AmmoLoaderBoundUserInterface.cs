// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using Content.Shared._Lua.AmmoLoader;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Lua.AmmoLoader.UI;

[UsedImplicitly]
public sealed class AmmoLoaderBoundUserInterface : BoundUserInterface
{
    private AmmoLoaderWindow? _window;

    public AmmoLoaderBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<AmmoLoaderWindow>();
        _window.OnUnloadOne += (protoId, emptyOnly) => SendMessage(new AmmoLoaderUnloadOneMessage(protoId, emptyOnly));
        _window.OnLoadTurret += (turret, protoId) => SendMessage(new AmmoLoaderLoadTurretMessage(turret, protoId));
        _window.OnUnloadTurret += turret => SendMessage(new AmmoLoaderUnloadTurretMessage(turret));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is AmmoLoaderBoundUserInterfaceState ammoState) _window?.UpdateState(ammoState);
    }
}

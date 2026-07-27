using Content.Shared.Actions;
using Robust.Shared.Serialization;

namespace Content.Shared._Forge.Clothing;

[Serializable, NetSerializable]
[Flags]
public enum ModsuitGauntletEnabledSlots : byte
{
    None = 0,
    Urk = 1 << 0,
    Omnitool = 1 << 1,
    Welder = 1 << 2,
    NaniteApplicator = 1 << 3,
    Auxiliary = 1 << 4,
    Piping = 1 << 5,
    All = Urk | Omnitool | Welder | NaniteApplicator,
}

[Serializable, NetSerializable]
public enum ModsuitGauntletToolSlot : byte
{
    Urk,
    Omnitool,
    Welder,
    NaniteApplicator,
    Auxiliary,
    Piping,
}

[Serializable, NetSerializable]
public sealed class ModsuitGauntletToggleToolMessage(NetEntity gauntlets, ModsuitGauntletToolSlot slot) : EntityEventArgs
{
    public readonly NetEntity Gauntlets = gauntlets;
    public readonly ModsuitGauntletToolSlot Slot = slot;
}

public sealed partial class OpenModsuitGauntletToolsMenuActionEvent : InstantActionEvent;

// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using Content.Shared._Mono.AmmoLoader;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Lua.AmmoLoader;

[Serializable, NetSerializable]
public enum AmmoLoaderUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class AmmoLoaderUnloadOneMessage : BoundUserInterfaceMessage
{
    public readonly EntProtoId PrototypeId;
    public readonly bool EmptyOnly;

    public AmmoLoaderUnloadOneMessage(EntProtoId prototypeId, bool emptyOnly = false)
    {
        PrototypeId = prototypeId;
        EmptyOnly = emptyOnly;
    }
}

[Serializable, NetSerializable]
public sealed class AmmoLoaderEjectAllMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class AmmoLoaderLoadTurretMessage : BoundUserInterfaceMessage
{
    public readonly NetEntity Turret;
    public readonly EntProtoId AmmoPrototypeId;

    public AmmoLoaderLoadTurretMessage(NetEntity turret, EntProtoId ammoPrototypeId)
    {
        Turret = turret;
        AmmoPrototypeId = ammoPrototypeId;
    }
}

[Serializable, NetSerializable]
public sealed class AmmoLoaderUnloadTurretMessage : BoundUserInterfaceMessage
{
    public readonly NetEntity Turret;

    public AmmoLoaderUnloadTurretMessage(NetEntity turret)
    {
        Turret = turret;
    }
}

[Serializable, NetSerializable]
public sealed class AmmoLoaderInventoryGroup
{
    public EntProtoId PrototypeId;
    public int Count;
    public bool IsEmpty;

    public AmmoLoaderInventoryGroup()
    {
    }

    public AmmoLoaderInventoryGroup(EntProtoId prototypeId, int count, bool isEmpty = false)
    {
        PrototypeId = prototypeId;
        Count = count;
        IsEmpty = isEmpty;
    }
}

[Serializable, NetSerializable]
public sealed class AmmoLoaderLinkedTurret
{
    public NetEntity Turret;
    public EntProtoId TurretPrototype;
    public string TurretName = string.Empty;
    public EntProtoId? LoadedAmmoPrototype;
    public int AmmoCount;
    public int AmmoCapacity;
    public bool CanModifyAmmo;

    public AmmoLoaderLinkedTurret()
    {
    }

    public AmmoLoaderLinkedTurret(
        NetEntity turret,
        EntProtoId turretPrototype,
        string turretName,
        EntProtoId? loadedAmmoPrototype,
        int ammoCount,
        int ammoCapacity,
        bool canModifyAmmo)
    {
        Turret = turret;
        TurretPrototype = turretPrototype;
        TurretName = turretName;
        LoadedAmmoPrototype = loadedAmmoPrototype;
        AmmoCount = ammoCount;
        AmmoCapacity = ammoCapacity;
        CanModifyAmmo = canModifyAmmo;
    }
}

[Serializable, NetSerializable]
public sealed class AmmoLoaderBoundUserInterfaceState : BoundUserInterfaceState
{
    public List<AmmoLoaderInventoryGroup> Groups = new();
    public List<AmmoLoaderLinkedTurret> LinkedTurrets = new();
    public int CurrentCount;
    public int MaxCapacity;
    public int MaxConnections;

    public AmmoLoaderBoundUserInterfaceState()
    {
    }

    public AmmoLoaderBoundUserInterfaceState(
        List<AmmoLoaderInventoryGroup> groups,
        List<AmmoLoaderLinkedTurret> linkedTurrets,
        int currentCount,
        int maxCapacity,
        int maxConnections)
    {
        Groups = groups;
        LinkedTurrets = linkedTurrets;
        CurrentCount = currentCount;
        MaxCapacity = maxCapacity;
        MaxConnections = maxConnections;
    }
}

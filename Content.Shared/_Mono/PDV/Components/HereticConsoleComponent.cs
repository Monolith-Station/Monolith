using Content.Shared.Contraband;
using Robust.Shared.Serialization;

namespace Content.Shared._Mono.PDV.Components;

[RegisterComponent]
public sealed partial class HereticConsoleComponent : Component;

[NetSerializable, Serializable]
public sealed class HereticConsoleState : BoundUserInterfaceState
{
    public List<Heretic> Heretics;

    public HereticConsoleState(List<Heretic> heretics)
    {
        Heretics = heretics;
    }
}

[NetSerializable, Serializable]
public enum HereticConsoleUiKey : byte
{
    Heretics
}

[NetSerializable, Serializable]
public sealed partial class Heretic
{
    public int Reward = 0;
    public string Name;

    public Heretic(int reward, string name)
    {
        Reward = reward;
        Name = name;
    }
}

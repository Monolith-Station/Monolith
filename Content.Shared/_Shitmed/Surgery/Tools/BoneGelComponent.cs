using Robust.Shared.GameStates;

namespace Content.Shared._Shitmed.Medical.Surgery.Tools;

[RegisterComponent, NetworkedComponent]
public sealed partial class BoneGelComponent : Component, ISurgeryToolComponent
{
    public string ToolName => "gel de osso"; // Andromeda: por que, deus?

    public bool? Used { get; set; } = null;

    [DataField]
    public float Speed { get; set; } = 1f;
}

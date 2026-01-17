using Robust.Shared.GameStates;

namespace Content.Shared._Mono.Claws;

/// <summary>
/// This is used for clipping nails (Claws). See <see cref="NailClipperDoAfterEvent"/>
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class NailCutterComponent : Component
{
    [DataField]
    public TimeSpan ClipDoAfter = TimeSpan.FromSeconds(10);

    [DataField]
    public int StageReduction = 1;

    [DataField]
    public float DeclawChance;
}

using Robust.Shared.GameStates;

namespace Content.Shared._Mono.BlackFlash;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BlackFlashImpactFramesComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan Start;

    [DataField, AutoNetworkedField]
    public TimeSpan FrameTime = TimeSpan.FromSeconds(0.045);

    [DataField, AutoNetworkedField]
    public TimeSpan RedFrameTime = TimeSpan.FromSeconds(0.075);

    [DataField, AutoNetworkedField]
    public int RedFrames = 1;

    [DataField, AutoNetworkedField]
    public int WhiteFrames = 4;

    [DataField]
    public Color RedFill = Color.FromHex("#c9101a");

    [DataField]
    public Color WhiteFill = Color.FromHex("#f6f2ef");

    [DataField]
    public Color LineColor = Color.FromHex("#0a0507");
    public TimeSpan RedSpan => RedFrameTime * RedFrames;

    public TimeSpan Total => RedSpan + FrameTime * WhiteFrames;
}

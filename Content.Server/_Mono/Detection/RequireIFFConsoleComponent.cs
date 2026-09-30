using Content.Shared.Shuttles.Components;

namespace Content.Server._Mono.Detection;

/// <summary>
/// This grid will not keep its IFF status if an IFF console on it is unanchored or destroyed.
/// </summary> 
[RegisterComponent]
public sealed partial class RequireIFFConsoleComponent : Component
{
    [DataField]
    public IFFFlags RemoveFlags = IFFFlags.HideLabel;
}
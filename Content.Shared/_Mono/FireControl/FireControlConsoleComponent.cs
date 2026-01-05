namespace Content.Shared._Mono.FireControl;

/// <summary>
/// These are for the consoles that provide the user interface for fire control servers.
/// </summary>
[RegisterComponent]
public sealed partial class FireControlConsoleComponent : Component
{
    [ViewVariables]
    public EntityUid? ConnectedServer = null;

    /// <summary>
    /// When we last made an admin log of someone firing using this console.
    /// Used to not put too much strain on server performance.
    /// </summary>
    [ViewVariables]
    public TimeSpan? NextLog = null;

    [DataField]
    public TimeSpan LogSpacing = TimeSpan.FromSeconds(1);

    [DataField]
    public float LogGridLookupRange = 1024f;
}

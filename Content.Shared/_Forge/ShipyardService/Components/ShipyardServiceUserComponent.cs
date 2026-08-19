namespace Content.Shared._Forge.ShipyardService.Components;

/// <summary>
/// Temporary session on a player while the drydock console is open, granting click-to-upgrade.
/// </summary>
[RegisterComponent]
public sealed partial class ShipyardServiceUserComponent : Component
{
    public EntityUid Console;

    public EntityUid? ActionEntity;
}

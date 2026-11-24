using System.Linq;
using System.Threading.Tasks;
using Content.Server._Mono.Planets;
using Content.Server.Administration;
using Content.Server.Administration.Managers;
using Content.Server.EUI;
using Content.Shared._Mono.Persistence;
using Content.Shared.Administration;
using Content.Shared.Eui;
using Robust.Shared.Console;
using Robust.Shared.Network;

namespace Content.Server._Mono.Persistence;

[AdminCommand(AdminFlags.Debug | AdminFlags.Mapping)]
public sealed partial class PersistencePanelCommand : LocalizedCommands
{
    internal const AdminFlags RequiredFlags = AdminFlags.Debug | AdminFlags.Mapping;
    [Dependency] private EuiManager _euis = default!;
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IAdminManager _admins = default!;

    public override string Command => "persistencepanel";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player is not { } player)
            shell.WriteError(Loc.GetString("shell-cannot-run-command-from-server"));
        else if (args.Length != 0)
            shell.WriteError(Help);
        else if (_admins.HasAdminFlag(player, RequiredFlags))
            _euis.OpenEui(new PersistencePanelEui(_admins, _entities.System<PlanetPersistenceSystem>(),
                _entities.System<PersistentProfileSystem>()), player);
    }
}

public sealed class PersistencePanelEui(IAdminManager admins, PlanetPersistenceSystem planets, PersistentProfileSystem profiles) : BaseEui
{
    private string? _status;
    private bool _busy;
    private NetUserId? _viewedUser;
    private PersistenceCharacter[] _characters = [];
    private bool CanUse => admins.HasAdminFlag(Player, PersistencePanelCommand.RequiredFlags);

    public override void Opened()
    {
        admins.OnPermsChanged += OnPermsChanged;
        planets.PanelChanged += StateDirty;
        StateDirty();
    }

    public override void Closed()
    {
        admins.OnPermsChanged -= OnPermsChanged;
        planets.PanelChanged -= StateDirty;
    }

    public override EuiStateBase GetNewState() =>
        CanUse ? new PersistencePanelState(planets.GetPanelPlanets(), _status) : new PersistencePanelState([], null);

    public override void HandleMessage(EuiMessageBase msg)
    {
        if (!CanUse)
        {
            Close();
            return;
        }

        switch (msg)
        {
            case FindPersistenceUser search:
                FindUser(search.Username);
                break;
            case AddPersistenceEntry entry:
                UpdateEntries(entry.Slot, (user, target) => profiles.AddPanelEntry(user, target.Preview.Name, entry));
                break;
            case RemovePersistenceItem remove:
                UpdateEntries(remove.Slot, (user, target) =>
                    (uint) remove.Index < (uint) target.Items.Length
                        ? profiles.RemovePanelItem(user, remove.Slot, target.Preview.Name, remove.Index, target.Items[remove.Index])
                        : Task.FromResult(new PersistenceEntriesUpdated(null, Loc.GetString("persistence-panel-item-changed"))));
                break;
            case SetPlanetSavesEnabled toggle:
                _status = null;
                planets.SetSavesEnabled(toggle.PlanetId, toggle.Enabled, Player);
                StateDirty();
                break;
            case DeletePlanetSave delete:
                _status = planets.DeletePanelSave(delete.PlanetId, delete.SaveId, delete.SavedAt, Player);
                StateDirty();
                break;
            default:
                base.HandleMessage(msg);
                break;
        }
    }

    private async void FindUser(string username)
    {
        if (_busy || string.IsNullOrWhiteSpace(username) || username.Length > 64)
            return;

        _busy = true;
        _viewedUser = null;
        _characters = [];
        try
        {
            var (user, result) = await profiles.ReadPanelUser(username.Trim());
            _viewedUser = user;
            _characters = result.Characters;
            if (!IsShutDown && CanUse)
                SendMessage(result);
        }
        catch (Exception ex)
        {
            Logger.ErrorS("persistence", $"Could not read profiles for the persistence panel: {ex}");
            if (!IsShutDown && CanUse)
                SendMessage(new PersistenceUserResult(null, [], failed: true));
        }
        finally
        {
            _busy = false;
        }
    }

    private async void UpdateEntries(int slot, Func<NetUserId, PersistenceCharacter, Task<PersistenceEntriesUpdated>> update)
    {
        if (_busy || _viewedUser is not { } user ||
            _characters.FirstOrDefault(character => character.Slot == slot) is not { } target)
            return;

        _busy = true;
        try
        {
            var result = await update(user, target);
            if (result.Character is { } updated)
                _characters[Array.IndexOf(_characters, target)] = updated;
            if (!IsShutDown && CanUse)
                SendMessage(result);
        }
        catch (Exception ex)
        {
            Logger.ErrorS("persistence", $"Could not update persistent data for {user}: {ex}");
            if (!IsShutDown && CanUse)
                SendMessage(new PersistenceEntriesUpdated(null, Loc.GetString("persistence-panel-entry-save-error")));
        }
        finally
        {
            _busy = false;
        }
    }

    private void OnPermsChanged(AdminPermsChangedEventArgs args)
    {
        if (args.Player == Player && !CanUse)
            Close();
    }
}

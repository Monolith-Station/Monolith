using System.Linq;
using System.Text;
using Content.Server.Administration;
using Content.Server.Administration.Managers;
using Content.Server.EUI;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._FarHorizons.StarSystem;
using Content.Shared._Mono.Planets;
using Content.Shared.Administration;
using Content.Shared.Eui;
using Robust.Shared.Console;
using Robust.Shared.Map.Components;

namespace Content.Server._Mono.Planets;

[AdminCommand(AdminFlags.Admin)]
public sealed class PlanetViewerCommand : LocalizedCommands
{
    [Dependency] private readonly EuiManager _eui = default!;

    public override string Command => "planetviewer";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player is not { } player)
        {
            shell.WriteError(Loc.GetString("shell-cannot-run-command-from-server"));
            return;
        }

        _eui.OpenEui(new PlanetViewerEui(), player);
    }
}

public sealed class PlanetViewerEui : BaseEui
{
    [Dependency] private readonly IEntityManager _entities = default!;
    [Dependency] private readonly IAdminManager _admins = default!;

    public PlanetViewerEui()
    {
        IoCManager.InjectDependencies(this);
    }

    public override void Opened()
    {
        _admins.OnPermsChanged += OnPermsChanged;
        StateDirty();
    }

    public override void Closed()
    {
        _admins.OnPermsChanged -= OnPermsChanged;
    }

    private void OnPermsChanged(AdminPermsChangedEventArgs args)
    {
        if (args.Player == Player && !_admins.HasAdminFlag(Player, AdminFlags.Admin))
            Close();
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);
        if (msg is not PlanetViewerRefreshMessage)
            return;

        if (!_admins.HasAdminFlag(Player, AdminFlags.Admin))
        {
            Close();
            return;
        }

        StateDirty();
    }

    public override EuiStateBase GetNewState()
    {
        if (!_admins.HasAdminFlag(Player, AdminFlags.Admin))
            return new PlanetViewerEuiState(string.Empty);

        var text = new StringBuilder();
        var planets = _entities.EntityQuery<PlanetBodyComponent, MetaDataComponent>()
            .OrderBy(p => p.Item2.EntityName).ToList();
        text.AppendLine($"Planets: {planets.Count}");
        foreach (var (planet, metadata) in planets)
        {
            text.AppendLine();
            text.AppendLine($"{Describe(metadata.Owner)} | Type: {planet.Type}");
            text.AppendLine($"  Star system: {Describe(planet.StarSystemMap)}");
            if (planet.SurfaceNetwork is not { } network)
            {
                text.AppendLine("  Z network: none");
                continue;
            }

            text.AppendLine($"  Z network: {Describe(network)}");
            if (!_entities.TryGetComponent<CEZMapNetworkComponent>(network, out var levels))
            {
                text.AppendLine("    Missing Z network component");
                continue;
            }

            text.AppendLine($"    Layers: {levels.ZLevels.Count}");
            foreach (var (z, map) in levels.ZLevels.OrderByDescending(pair => pair.Key))
            {
                var description = map is { } uid ? Describe(uid) : "empty slot";
                if (map is { } mapUid && _entities.TryGetComponent<MapComponent>(mapUid, out var mapComp))
                    description += $" | Map ID: {mapComp.MapId}";
                text.AppendLine($"    Z {z}: {description}");
            }
        }

        return new PlanetViewerEuiState(text.ToString());
    }

    private string Describe(EntityUid uid)
    {
        return _entities.TryGetComponent<MetaDataComponent>(uid, out var metadata)
            ? $"{metadata.EntityName} ({_entities.GetNetEntity(uid)})"
            : $"missing entity ({uid})";
    }
}

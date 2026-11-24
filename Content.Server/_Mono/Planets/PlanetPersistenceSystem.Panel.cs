using System.Globalization;
using System.IO;
using System.Linq;
using Content.Shared._FarHorizons.StarSystem;
using Content.Shared._FarHorizons.StarSystem.Prototypes;
using Content.Shared._Mono.Persistence;
using Robust.Shared.ContentPack;
using Robust.Shared.Player;
using Robust.Shared.Utility;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Content.Server._Mono.Planets;

public sealed partial class PlanetPersistenceSystem
{
    internal event Action? PanelChanged;

    private void OnSurfaceTerminating(Entity<PlanetMapGenerationComponent> ent, ref EntityTerminatingEvent ev) =>
        PanelChanged?.Invoke();

    private TimeSpan? NextSaveAt(string id)
    {
        var interval = _nextSaveOverrides.GetValueOrDefault(id, _saveInterval);
        return _persistPlanets && _roundStartedAt != null && !_roundEnded && !_pausedSaves.ContainsKey(id) &&
               interval > TimeSpan.Zero
            ? _lastSaves.GetValueOrDefault(id, _roundStartTime) + interval
            : null;
    }

    internal void SetSavesEnabled(string id, bool enabled, ICommonSession admin)
    {
        if (!_prototypes.TryIndex<PlanetTypePrototype>(id, out var planet) || !CanPersist(planet))
            return;

        var now = _timing.CurTime;
        if (!enabled)
        {
            if (!_pausedSaves.TryAdd(id, now))
                return;
        }
        else
        {
            if (!_pausedSaves.Remove(id, out var pausedAt))
                return;

            if (_roundStartedAt != null)
            {
                var lastSave = _lastSaves.GetValueOrDefault(id, _roundStartTime);
                _lastSaves[id] = lastSave + (now - (lastSave > pausedAt ? lastSave : pausedAt));
            }
        }

        _chat.SendAdminAnnouncement(Loc.GetString(
            enabled ? "persistence-panel-enabled-admin" : "persistence-panel-disabled-admin",
            ("admin", admin.Name), ("planet", planet.Name)));
        PanelChanged?.Invoke();
    }

    internal string? DeletePanelSave(string planetId, string saveId, string savedAt, ICommonSession admin)
    {
        if (!_prototypes.TryIndex<PlanetTypePrototype>(planetId, out var planet) || !CanPersist(planet) ||
            !TryGetRoundId(saveId, out _) ||
            !GetPanelSaves(planetId).Any(save => save.Id == saveId && save.SavedAt == savedAt))
            return Loc.GetString("planet-save-delete-not-found");

        try
        {
            return DeleteSave(planetId, saveId, confirm: true, admin: admin);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            _sawmill.Error($"Could not delete save {saveId} for {planetId}: {e}");
            return Loc.GetString("planet-save-delete-failed");
        }
    }

    internal PlanetPersistenceSummary[] GetPanelPlanets()
    {
        var loaded = new HashSet<string>();
        var planets = AllEntityQuery<PlanetBodyComponent>();
        while (planets.MoveNext(out var body))
        {
            if (TryComp<PlanetMapGenerationComponent>(body.SurfaceNetwork, out var generation) &&
                !TerminatingOrDeleted(generation.Owner))
                loaded.Add(body.Type.Id);
        }

        return _prototypes.EnumeratePrototypes<PlanetTypePrototype>()
            .Where(CanPersist)
            .OrderBy(planet => planet.Name)
            .Select(planet => new PlanetPersistenceSummary(planet.ID, planet.Name, planet.Persistence,
                loaded.Contains(planet.ID), planet.PersistenceFlags.ToArray(), !_pausedSaves.ContainsKey(planet.ID),
                loaded.Contains(planet.ID) ? NextSaveAt(planet.ID) : null, GetPanelSaves(planet.ID)))
            .ToArray();
    }

    private PlanetSaveSummary[] GetPanelSaves(string planetId)
    {
        if (!ResPath.IsValidFilename(planetId))
            return [];

        const int limit = 20;
        var saves = new List<PlanetSaveSummary>();
        var data = _resources.UserData;
        var folder = Root / planetId;
        try
        {
            if (!data.IsDir(folder))
                return [];

            foreach (var entry in data.DirectoryEntries(folder).OrderByDescending(name => name, StringComparer.Ordinal))
            {
                if (!TryGetRoundId(entry, out var round))
                    continue;

                var path = folder / entry / "metadata.yml";
                if (!data.Exists(path))
                    continue;

                try
                {
                    using var reader = data.OpenText(path);
                    var yaml = new YamlStream();
                    yaml.Load(reader);
                    if (yaml.Documents.Count != 1 || yaml.Documents[0].RootNode is not YamlMappingNode mapping ||
                        !int.TryParse(Field("roundId"), CultureInfo.InvariantCulture, out var savedRound) || savedRound != round ||
                        !int.TryParse(Field("seed"), CultureInfo.InvariantCulture, out var seed))
                        continue;

                    saves.Add(new PlanetSaveSummary(entry, round, seed, bool.TryParse(Field("final"), out var final) && final,
                        Field("savedAt") ?? "-", Field("persistence") ?? "-", CanDeleteSave(planetId, entry)));
                    if (saves.Count == limit)
                        break;

                    string? Field(string name) => mapping.Children.TryGetValue(new YamlScalarNode(name), out var node)
                        ? (node as YamlScalarNode)?.Value
                        : null;
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or YamlException)
                {
                    _sawmill.Warning($"Could not read planet metadata at {path}: {e.Message}");
                }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            _sawmill.Warning($"Could not list planet saves in {folder}: {e.Message}");
        }

        return saves.ToArray();
    }
}

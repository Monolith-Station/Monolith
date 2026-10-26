using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server.Power.EntitySystems;
using Content.Shared._Mono.Cloning;
using Content.Shared.Mind;
using Robust.Server.Player;
using Robust.Shared.Prototypes;
using Content.Server._NF.CryoSleep;

namespace Content.Server._Mono.Cloning;

public sealed partial class GeneticDatabaseSystem : EntitySystem
{
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private SharedMindSystem _mindSystem = default!;
    [Dependency] private CryoSleepSystem _cryoSleep = default!;

    private readonly HashSet<Entity<GeneticDatabaseComponent>> _found = new();

    /// <summary>
    /// Sorry for calling it a db
    /// </summary>
    public bool TryGetDatabase(EntityUid client, [NotNullWhen(true)] out Entity<GeneticDatabaseComponent>? db)
    {
        db = null;
        var comp = EnsureComp<GeneticDatabaseClientComponent>(client);
        var grid = Transform(client).GridUid;
        if (grid == null)
        {
            comp.Database = null;
            return false;
        }

        if (comp.Database is { } cached &&
            TryComp<GeneticDatabaseComponent>(cached, out var cachedComp) &&
            IsUsable(cached, grid.Value))
        {
            db = (cached, cachedComp);
            return true;
        }

        _found.Clear();
        _lookup.GetChildEntities(grid.Value, _found);
        foreach (var candidate in _found)
        {
            if (!IsUsable(candidate, grid.Value))
                continue;

            comp.Database = candidate;
            db = candidate;
            return true;
        }

        comp.Database = null;
        return false;
    }

    private bool IsUsable(EntityUid database, EntityUid grid)
    {
        var xform = Transform(database);
        return xform.GridUid == grid && xform.Anchored && this.IsPowered(database, EntityManager);
    }

    /// <summary>
    /// Upsert is a combination of update and insert by the way. Also, a combination of those kind of words
    /// is called a portmanteau btw. pretty cool, right?
    /// </summary>
    public void Upsert(Entity<GeneticDatabaseComponent> db, GeneticRecord record)
    {
        record.Id = db.Comp.Records.TryGetValue(record.Mind, out var existing)
            ? existing.Id
            : db.Comp.NextId++;

        db.Comp.Records[record.Mind] = record;
    }

    public bool HasRecord(Entity<GeneticDatabaseComponent> db, EntityUid mind)
    {
        return db.Comp.Records.ContainsKey(mind);
    }

    public bool TryGetRecord(Entity<GeneticDatabaseComponent> db, int id, [NotNullWhen(true)] out GeneticRecord? record)
    {
        foreach (var candidate in db.Comp.Records.Values)
        {
            if (candidate.Id != id)
                continue;

            record = candidate;
            return true;
        }

        record = null;
        return false;
    }

    public bool Remove(Entity<GeneticDatabaseComponent> db, GeneticRecord record)
    {
        return db.Comp.Records.Remove(record.Mind);
    }

    public GeneticRecordStatus GetStatus(GeneticRecord record)
    {
        if (!TryComp<MindComponent>(record.Mind, out var mind))
            return GeneticRecordStatus.Lost;

        if ((mind.UserId ?? mind.OriginalOwnerUserId) is { } user && _cryoSleep.HasCryosleepingBody(user))
            return GeneticRecordStatus.MovedOn;

        if (mind.UserId == null)
        {
            if (mind.OriginalOwnerUserId is { } original &&
                _mindSystem.TryGetMind(original, out var currentMind, out _) &&
                currentMind != record.Mind)
            {
                return GeneticRecordStatus.MovedOn;
            }

            return GeneticRecordStatus.Offline;
        }

        if (!_playerManager.TryGetSessionById(mind.UserId.Value, out _))
            return GeneticRecordStatus.Offline;

        if (_mindSystem.TryGetMind(mind.UserId.Value, out var userMind, out _) && userMind != record.Mind)
            return GeneticRecordStatus.MovedOn;

        if (!_mindSystem.IsCharacterDeadPhysically(mind))
            return GeneticRecordStatus.Alive;

        return GeneticRecordStatus.Cloneable;
    }

    public List<GeneticRecordEntry> GetEntries(Entity<GeneticDatabaseComponent> db)
    {
        return db.Comp.Records.Values
            .Select(r => new GeneticRecordEntry(r.Id, r.Name, GetSpeciesName(r), GetStatus(r), r.ScannedAt))
            .OrderBy(e => e.Name)
            .ThenBy(e => e.Id)
            .ToList();
    }

    private string GetSpeciesName(GeneticRecord record)
    {
        return _prototype.TryIndex(record.Species, out var species)
            ? Loc.GetString(species.Name)
            : record.Species.Id;
    }
}

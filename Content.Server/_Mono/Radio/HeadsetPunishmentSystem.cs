using Content.Server.Explosion.EntitySystems;
using Content.Server.Stunnable;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server._Mono.Radio;

public sealed partial class HeadsetPunishmentSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private ExplosionSystem _explosions = default!;
    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private StunSystem _stun = default!;

    private readonly HashSet<string> _activePrototypes = new(StringComparer.OrdinalIgnoreCase) { "Default" };

    [ViewVariables]
    public IReadOnlyCollection<string> ActivePrototypes => _activePrototypes;

    [ViewVariables(VVAccess.ReadWrite)]
    public string AddPrototypeByName
    {
        get => string.Empty;
        set => AddPrototype(value);
    }

    [ViewVariables(VVAccess.ReadWrite)]
    public string RemovePrototypeByName
    {
        get => string.Empty;
        set => RemovePrototype(value);
    }

    public bool AddPrototype(string id)
    {
        if (!_prototypes.HasIndex<HeadsetPunishmentPrototype>(id))
            return false;

        return _activePrototypes.Add(id);
    }

    public bool RemovePrototype(string id)
    {
        return _activePrototypes.Remove(id);
    }

    public void Punish(EntityUid wearer, EntityUid headset, string message)
    {
        var epicenter = Transform(headset).Coordinates;
        var stunSeconds = 0f;
        var punished = false;

        foreach (var id in _activePrototypes)
        {
            if (!_prototypes.TryIndex<HeadsetPunishmentPrototype>(id, out var config))
                continue;

            var matches = 0;
            foreach (var word in config.Words)
                matches += CountMatches(message, word);

            if (matches == 0)
                continue;

            punished = true;
            stunSeconds += config.StunSecondsPerMatch * matches;
            var intensity = Math.Min(config.MaxIntensity,
                config.BaseIntensity + config.IntensityPerMatch * (matches - 1));

            if (intensity <= 0f)
                continue;

            _explosions.QueueExplosion(epicenter, config.Explosion, intensity,
                intensity * 10f, intensity, wearer, tileBreakScale: 0f,
                maxTileBreak: 0, canCreateVacuum: false);

            var damage = new DamageSpecifier();
            damage.DamageDict.Add("Heat", intensity * 2f);
            damage.DamageDict.Add("Blunt", intensity);
            _damage.TryChangeDamage(wearer, damage, origin: wearer);
        }

        if (!punished)
            return;

        _stun.TryParalyze(wearer, TimeSpan.FromSeconds(stunSeconds), true);
        QueueDel(headset);
    }

    private static int CountMatches(string message, string word)
    {
        if (string.IsNullOrWhiteSpace(word))
            return 0;

        var count = 0;
        var start = 0;
        while ((start = message.IndexOf(word, start, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            var end = start + word.Length;
            if ((start == 0 || !char.IsLetterOrDigit(message[start - 1])) &&
                (end == message.Length || !char.IsLetterOrDigit(message[end])))
                count++;
            start = end;
        }

        return count;
    }
}

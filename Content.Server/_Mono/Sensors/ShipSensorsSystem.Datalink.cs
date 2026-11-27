using Content.Shared._Mono.Sensors;

namespace Content.Server._Mono.Sensors;

public sealed partial class ShipSensorsSystem
{
    private void PropagateDatalinks()
    {
        var now = _timing.CurTime;

        foreach (var list in _datalinkByCode.Values)
            list.Clear();

        var q = EntityQueryEnumerator<GridSensorsComponent>();
        while (q.MoveNext(out var uid, out var comp))
        {
            foreach (var code in comp.DatalinkCodes)
            {
                if (string.IsNullOrWhiteSpace(code))
                    continue;

                var key = NormalizeCode(code);
                if (!_datalinkByCode.TryGetValue(key, out var list))
                {
                    list = new List<(EntityUid, GridSensorsComponent)>();
                    _datalinkByCode[key] = list;
                }

                list.Add((uid, comp));
            }
        }

        const SensorContactFlags localSensorMask =
            SensorContactFlags.ActiveRadar
            | SensorContactFlags.PassiveRadar
            | SensorContactFlags.Infrared
            | SensorContactFlags.Visual;

        foreach (var (_, members) in _datalinkByCode)
        {
            if (members.Count < 2)
                continue;

            _datalinkUnion.Clear();

            foreach (var (_, comp) in members)
            {
                foreach (var net in comp.LocallySensed)
                {
                    if (!comp.Contacts.TryGetValue(net, out var contact))
                        continue;

                    if ((contact.Flags & localSensorMask) == 0)
                        continue;

                    var export = contact with
                    {
                        Flags = contact.Flags & localSensorMask,
                        LastSeen = now,
                    };

                    if (_datalinkUnion.TryGetValue(net, out var existing))
                    {
                        _datalinkUnion[net] = existing with
                        {
                            Level = BestLevel(existing.Level, export.Level),
                            Flags = existing.Flags | export.Flags,
                            LastSeen = now,
                            WorldPosition = export.WorldPosition,
                            Designation = export.Designation ?? existing.Designation,
                            Range = export.Range,
                        };
                    }
                    else
                    {
                        _datalinkUnion[net] = export;
                    }
                }
            }

            foreach (var (uid, comp) in members)
            {
                var changed = false;
                var selfNet = GetNetEntity(uid);
                if (!_gridWorldPos.TryGetValue(uid, out var ourPos))
                    ourPos = _xform.GetWorldPosition(uid);

                foreach (var (net, contact) in _datalinkUnion)
                {
                    if (net == selfNet)
                        continue;

                    if (comp.LocallySensed.Contains(net))
                    {
                        if (!comp.Contacts.TryGetValue(net, out var own))
                            continue;

                        var tagged = own with
                        {
                            Flags = (own.Flags & localSensorMask) | SensorContactFlags.Datalink,
                        };
                        if (tagged != own)
                        {
                            comp.Contacts[net] = tagged;
                            changed = true;
                        }
                        continue;
                    }

                    var targetPos = contact.WorldPosition;
                    var range = (targetPos - ourPos).Length();

                    if (comp.Contacts.TryGetValue(net, out var existing))
                    {
                        var localBits = existing.Flags & localSensorMask;
                        var merged = existing with
                        {
                            Level = BestLevel(existing.Level, contact.Level),
                            Flags = localBits | SensorContactFlags.Datalink,
                            LastSeen = now,
                            WorldPosition = targetPos,
                            Range = range,
                            Designation = contact.Designation ?? existing.Designation,
                        };

                        if (merged != existing)
                        {
                            comp.Contacts[net] = merged;
                            changed = true;
                        }
                    }
                    else
                    {
                        comp.Contacts[net] = contact with
                        {
                            Flags = SensorContactFlags.Datalink,
                            LastSeen = now,
                            WorldPosition = targetPos,
                            Range = range,
                        };
                        changed = true;
                    }
                }

                if (changed)
                    Dirty(uid, comp);
            }
        }
    }
}

using Content.Shared._Mono.Company;
using Robust.Shared.Prototypes;

namespace Content.Server._Mono.Company;

public struct CompanyMemberRecord
{
    public Guid PlayerUserId;
    public string LastSeenUserName;
    public bool Owner;
    public ProtoId<CompanyPrototype> Company;
}

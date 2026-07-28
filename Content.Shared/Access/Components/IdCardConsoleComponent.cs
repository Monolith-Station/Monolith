using Content.Shared.Access.Systems;
using Content.Shared.Containers.ItemSlots;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Content.Shared.Roles; // Frontier

namespace Content.Shared.Access.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedIdCardConsoleSystem))]
public sealed partial class IdCardConsoleComponent : Component
{
    public const int MaxFullNameLength = 30;
    public const int MaxJobTitleLength = 30;

    public static string PrivilegedIdCardSlotId = "IdCardConsole-privilegedId";
    public static string TargetIdCardSlotId = "IdCardConsole-targetId";

    [DataField]
    public ItemSlot PrivilegedIdSlot = new();

    [DataField]
    public ItemSlot TargetIdSlot = new();

    [Serializable, NetSerializable]
    public sealed class WriteToTargetIdMessage : BoundUserInterfaceMessage
    {
        public readonly string FullName;
        public readonly string JobTitle;
        public readonly List<ProtoId<AccessLevelPrototype>> AccessList;
        public readonly ProtoId<JobPrototype> JobPrototype; // Frontier: AccessPrototype<JobPrototype

        public WriteToTargetIdMessage(string fullName, string jobTitle, List<ProtoId<AccessLevelPrototype>> accessList, ProtoId<JobPrototype> jobPrototype) // Frontier: jobProtoype - AccessPrototype<JobPrototype
        {
            FullName = fullName;
            JobTitle = jobTitle;
            AccessList = accessList;
            JobPrototype = jobPrototype;
        }
    }

    // Put this on shared so we just send the state once in PVS range rather than every time the UI updates.

    [DataField, AutoNetworkedField]
    public List<ProtoId<AccessLevelPrototype>> AccessLevels = new()
    {
    "Armory", //Used by TSF, unknown purpose
    //"Atmospherics",
    //"Bar",
    "Brig",
    "Detective",
    "Captain",
    //"Cargo",
    //"Chapel",
    //"Chemistry",
    "Chemistry", // DO NOT HIDE, it prevents all cards with chemistry from getting additional access (Ex: TSF Captain)
    //"ChiefEngineer",
    //"ChiefMedicalOfficer",
    "ChiefMedicalOfficer", //DO NOT HIDE, required for DOC
    "Command",
    //"Cryogenics",
    //"Engineering",
    "External",
    "HeadOfPersonnel",
    //"Hydroponics",
    "Janitor",
    //"Kitchen",
    //"Lawyer", //Monolith:  Not used
    "Mail", // Frontier
    //"Maintenance", //Monolith: Not used
    "Medical",
    "Mercenary", // Frontier
    //"Quartermaster",
    //"Research",
    //"ResearchDirector",
    //"Salvage",
    "Security",
    "Service",
    "StationTrafficController", // Frontier
    //"USSP", // Mono
    //"USSPHigh", // Mono
    //"USSPCommand", // Mono
    //"Theatre",
    "Frontier", // Frontier //TSF Base access
    "Sergeant", // Frontier //TSF FTL access
    "Bailiff", // Frontier //TSF Command access
    "TsfmcEngineering", // Mono, TSF Engineering access
    "HeadOfSecurity", // TSF Colonel
    "Pirate", //Frontier //PDV base access
    "PDVCommand", //Mono, PDV leadership (Denasvar, Asvaran, Vizier)
    "GrandVizier",//Mono, PDV Vizier
    };

    [Serializable, NetSerializable]
    public sealed class IdCardConsoleBoundUserInterfaceState : BoundUserInterfaceState
    {
        public readonly string PrivilegedIdName;
        public readonly bool IsPrivilegedIdPresent;
        public readonly bool IsPrivilegedIdAuthorized;
        public readonly bool IsTargetIdPresent;
        public readonly string TargetIdName;
        public readonly string? TargetIdFullName;
        public readonly string? TargetIdJobTitle;
        public readonly bool HasOwnedShuttle; // Frontier
        public readonly string?[]? TargetShuttleNameParts; // Frontier
        public readonly List<ProtoId<AccessLevelPrototype>>? TargetIdAccessList;
        public readonly List<ProtoId<AccessLevelPrototype>>? AllowedModifyAccessList;
        public readonly ProtoId<JobPrototype> TargetIdJobPrototype; // Frontier: AccessLevelPrototype<JobPrototype

        public IdCardConsoleBoundUserInterfaceState(bool isPrivilegedIdPresent,
            bool isPrivilegedIdAuthorized,
            bool isTargetIdPresent,
            string? targetIdFullName,
            string? targetIdJobTitle,
            bool hasOwnedShuttle,
            string?[]? targetShuttleNameParts,
            List<ProtoId<AccessLevelPrototype>>? targetIdAccessList,
            List<ProtoId<AccessLevelPrototype>>? allowedModifyAccessList,
            ProtoId<JobPrototype> targetIdJobPrototype, // Frontier: AccessLevelPrototype<JobPrototype
            string privilegedIdName,
            string targetIdName)
        {
            IsPrivilegedIdPresent = isPrivilegedIdPresent;
            IsPrivilegedIdAuthorized = isPrivilegedIdAuthorized;
            IsTargetIdPresent = isTargetIdPresent;
            TargetIdFullName = targetIdFullName;
            TargetIdJobTitle = targetIdJobTitle;
            HasOwnedShuttle = hasOwnedShuttle;
            TargetShuttleNameParts = targetShuttleNameParts;
            TargetIdAccessList = targetIdAccessList;
            AllowedModifyAccessList = allowedModifyAccessList;
            TargetIdJobPrototype = targetIdJobPrototype;
            PrivilegedIdName = privilegedIdName;
            TargetIdName = targetIdName;
        }
    }

    [Serializable, NetSerializable]
    public enum IdCardConsoleUiKey : byte
    {
        Key,
    }
}

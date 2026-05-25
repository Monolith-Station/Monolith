using Content.Server.Chat.Managers;
using Content.Server.IdentityManagement;
using Content.Shared.Chat;
using Content.Shared.Examine;
using Content.Shared.Inventory;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using System.Globalization;
using Content.Shared.CCVar;

namespace Content.Server._White.Examine
{
    public sealed partial class ExaminableCharacterSystem : EntitySystem
    {
        [Dependency] private InventorySystem _inventorySystem = default!;
        [Dependency] private IdentitySystem _identitySystem = default!;
        [Dependency] private EntityManager _entityManager = default!;
        [Dependency] private IChatManager _chatManager = default!;
        [Dependency] private INetConfigurationManager _netConfigManager = default!;

        public override void Initialize()
        {
            SubscribeLocalEvent<ExaminableCharacterComponent, ExaminedEvent>(HandleExamine);
        }

        private void HandleExamine(EntityUid uid, ExaminableCharacterComponent comp, ExaminedEvent args)
        {
            if (!TryComp<ActorComponent>(args.Examiner, out var actorComponent)
                || !args.IsInDetailsRange)
                return;

            var showExamine = _netConfigManager.GetClientCVar(actorComponent.PlayerSession.Channel, CCVars.DetailedExamine);

            var selfaware = args.Examiner == args.Examined;
            var logLines = new List<string>();

            string canseeloc = "examine-can-see";
            string nameloc = "examine-name";

            if (selfaware)
            {
                canseeloc += "-selfaware";
                nameloc += "-selfaware";
            }

            var identity = _identitySystem.GetEntityIdentity(uid);
            var name = Loc.GetString(nameloc, ("name", identity));
            logLines.Add($"[color=DarkGray][font size=10]{name}[/font][/color]");
            
            if (showExamine)
                args.PushMarkup($"[font size=10]{name}[/font]", 15);

            var cansee = Loc.GetString(canseeloc, ("ent", uid));
            logLines.Add($"[color=DarkGray][font size=10]{cansee}[/font][/color]");
            
            if (showExamine)
                args.PushMarkup($"[font size=10]{cansee}[/font]", 14);

            var slotLabels = new Dictionary<string, string>
            {
                { "head", "head-" },
                { "eyes", "eyes-" },
                { "mask", "mask-" },
                { "neck", "neck-" },
                { "ears", "ears-" },
                { "jumpsuit", "jumpsuit-" },
                { "outerClothing", "outer-" },
                { "back", "back-" },
                { "gloves", "gloves-" },
                { "belt", "belt-" },
                { "id", "id-" },
                { "shoes", "shoes-" },
                { "suitstorage", "suitstorage-" }
            };

            var priority = 13;

            foreach (var slotEntry in slotLabels)
            {
                var slotName = slotEntry.Key;
                var slotLabel = slotEntry.Value;

                slotLabel += "examine";

                if (selfaware)
                    slotLabel += "-selfaware";

                if (!_inventorySystem.TryGetSlotEntity(uid, slotName, out var slotEntity))
                    continue;

                if (_entityManager.TryGetComponent<MetaDataComponent>(slotEntity, out var metaData))
                {
                    var item = Loc.GetString(slotLabel, ("item", metaData.EntityName), ("ent", uid));
                    if (showExamine)
                        args.PushMarkup($"[font size=10]{item}[/font]", priority);
                    logLines.Add($"[color=DarkGray][font size=10]{item}[/font][/color]");
                    priority--;
                }
            }

            if (priority < 13)
            {
                // We already pushed the basic description above
            }
            else
            {
                string canseenothingloc = "examine-can-see-nothing";

                if (selfaware)
                    canseenothingloc += "-selfaware";

                var canseenothing = Loc.GetString(canseenothingloc, ("ent", uid));
                logLines.Add($"[color=DarkGray][font size=10]{canseenothing}[/font][/color]");
                
                if (showExamine)
                    args.PushMarkup($"[font size=10]{canseenothing}[/font]", priority);
            }

            var combinedLog = string.Join("\n", logLines);

            if (showExamine && _netConfigManager.GetClientCVar(actorComponent.PlayerSession.Channel, CCVars.LogInChat))
                _chatManager.ChatMessageToOne(ChatChannel.Emotes, combinedLog, combinedLog, EntityUid.Invalid, false, actorComponent.PlayerSession.Channel, recordReplay: false);
        }
    }
}

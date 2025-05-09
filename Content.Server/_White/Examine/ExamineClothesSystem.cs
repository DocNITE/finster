using Content.Server.Access.Systems;
using Content.Shared.Access.Components;
using Content.Shared.Examine;
using Robust.Shared.Enums;
using Content.Shared.Humanoid;
using Content.Shared.IdentityManagement.Components;
using Content.Shared.Inventory;
using Content.Shared.PDA;
using Robust.Server.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.Console;
using Robust.Shared.Player;
using Content.Shared.CCVar;

namespace Content.Server._White.Examine
{

    //^.^
    public sealed class ExamineClothesSystem : EntitySystem
    {
        [Dependency] private readonly InventorySystem _inventorySystem = default!;
        [Dependency] private readonly EntityManager _entityManager = default!;
        [Dependency] private readonly IdCardSystem _idCard = default!;
        [Dependency] private readonly IConsoleHost _consoleHost = default!;
        [Dependency] private readonly INetConfigurationManager _netConfigManager = default!;


        public static Dictionary<string, string> SlotLabels = new Dictionary<string, string>
            {
                { "head", "head-" },
                { "eyes", "eyes-" },
                { "mask", "mask-" },
                { "neck", "neck-" },
                { "ears", "ears-" },
                { "uniform2", "uniform2-" },
                { "uniform", "uniform-" },
                { "outerClothing", "outer-" },
                { "back", "back-" },
                { "back2", "back2-"},
                { "gloves", "gloves-" },
                { "belt", "belt-" },
                { "shoes", "shoes-" }
            };


        public override void Initialize()
        {
            SubscribeLocalEvent<ExaminableClothesComponent, ExaminedEvent>(HandleExamine);
        }

        private void SendNoticeMessage(ActorComponent actorComponent, string message)
        {
            var should = _netConfigManager.GetClientCVar(actorComponent.PlayerSession.Channel, CCVars.LogChatActions);

            if (should)
            {
                var formatedMessage = Loc.GetString("notice-command", ("fontsize", "10"), ("fontcolor", "aeabc4"), ("message", message));
                _consoleHost.RemoteExecuteCommand(actorComponent.PlayerSession, $"notice {formatedMessage}");
            }
        }

        private void HandleExamine(EntityUid uid, ExaminableClothesComponent comp, ExaminedEvent args)
        {
            var infoLines = new List<string>();

            var ev = new SeeIdentityAttemptEvent();
            RaiseLocalEvent(uid, ev);

            var idInfoString = GetInfo(uid);
            if (!string.IsNullOrEmpty(idInfoString))
            {
                //infoLines.Add(idInfoString);
                args.PushMarkup(idInfoString);
            }

            var examinedSlots = 0;
            string markup = "";

            foreach (var slotEntry in SlotLabels)
            {
                var slotName = slotEntry.Key;
                var slotLabel = slotEntry.Value;

                if (_entityManager.TryGetComponent<HumanoidAppearanceComponent>(uid, out var appearanceComponent))
                {
                    switch (appearanceComponent.Gender)
                    {
                        case Gender.Male:
                            slotLabel += "he";
                            break;
                        case Gender.Neuter:
                            slotLabel += "it";
                            break;
                        case Gender.Epicene:
                            slotLabel += "they";
                            break;
                        case Gender.Female:
                            slotLabel += "she";
                            break;
                    }
                }

                if (!_inventorySystem.TryGetSlotEntity(uid, slotName, out var slotEntity))
                    continue;

                if (_entityManager.TryGetComponent<MetaDataComponent>(slotEntity, out var metaData))
                {
                    examinedSlots++;
                    var item = $"{Loc.GetString(slotLabel)} [color=paleturquoise][bold]{metaData.EntityName}[/bold][/color].\n";
                    markup += item;
                    //infoLines.Add(item);
                }
            }

            if (markup != string.Empty)
                args.PushMarkup(markup, -2);

            //var combinedInfo = string.Join("\n", infoLines);

            //if (TryComp(args.Examiner, out ActorComponent? actorComponent))
            //{
            //    SendNoticeMessage(actorComponent, combinedInfo);
            //}
        }

        private int GetUsedSlotsCount(EntityUid uid)
        {
            var slots = 0;
            foreach (var slotEntry in SlotLabels)
            {
                var slotName = slotEntry.Key;

                if (!_inventorySystem.TryGetSlotEntity(uid, slotName, out var slotEntity))
                    continue;

                slots++;
            }

            return slots;
        }

        private string GetInfo(EntityUid uid)
        {
            if (_inventorySystem.TryGetSlotEntity(uid, "id", out var idUid))
            {
                // PDA
                if (EntityManager.TryGetComponent(idUid, out PdaComponent? pda) &&
                    TryComp<IdCardComponent>(pda.ContainedId, out var id))
                {
                    return GetNameAndJob(id);
                }
                // ID Card
                if (EntityManager.TryGetComponent(idUid, out id))
                {
                    return GetNameAndJob(id);
                }
            }
            return "";
        }

        private string GetNameAndJob(IdCardComponent id)
        {
            var jobSuffix = string.IsNullOrWhiteSpace(id.JobTitle) ? "" : $" ({id.JobTitle})";

            var val = string.IsNullOrWhiteSpace(id.FullName)
                ? Loc.GetString("access-id-card-component-owner-name-job-title-text",
                    ("jobSuffix", jobSuffix))
                : Loc.GetString("access-id-card-component-owner-full-name-job-title-text",
                    ("fullName", id.FullName),
                    ("jobSuffix", jobSuffix));

            return val;
        }
    }
}

using System.Linq;
using Content.Server.Verbs;
using Content.Shared.Examine;
using Content.Shared.Verbs;
using JetBrains.Annotations;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Server.Examine
{
    [UsedImplicitly]
    public sealed class ExamineSystem : ExamineSystemShared
    {
        [Dependency] private readonly VerbSystem _verbSystem = default!;

        private readonly FormattedMessage _entityNotFoundMessage = new();
        private readonly FormattedMessage _entityOutOfRangeMessage = new();

        public override void Initialize()
        {
            base.Initialize();
            _entityNotFoundMessage.AddText(Loc.GetString("examine-system-entity-does-not-exist"));
            _entityOutOfRangeMessage.AddText(Loc.GetString("examine-system-cant-see-entity"));

            SubscribeNetworkEvent<ExamineSystemMessages.RequestExamineInfoMessage>(ExamineInfoRequest);
        }

        public override void SendExamineMessage(
                EntityUid player,
                EntityUid target,
                FormattedMessage message,
                bool getVerbs,
                bool centerAtCursor,
                string? titleName = null)
        {
            if (!TryComp<ActorComponent>(player, out var actor))
                return;

            var session = actor.PlayerSession;

            SortedSet<Verb>? verbs = null;
            if (getVerbs)
                verbs = _verbSystem.GetLocalVerbs(target, player, typeof(ExamineVerb));

            var newMessage = new FormattedMessage();

            // And add some customizations
            PushFont(ref newMessage);
            newMessage.PushNewline();
            newMessage.PushTag(new MarkupNode("examineborder", null, null)); // border
            if (titleName is not null)
                PushTitleLine(ref newMessage, titleName);
            else if (player == target)
                PushExamineYourselfLine(ref newMessage);
            else
                PushEntityNameLine(ref newMessage, target, Name(target));

            newMessage.PushNewline();
            PushLine(ref newMessage);
            newMessage.PushNewline();

            newMessage.AddMessage(message);

            newMessage.PushNewline();
            PushLine(ref newMessage);
            newMessage.PushNewline();
            newMessage.Pop(); // border
            newMessage.Pop();

            var ev = new ExamineSystemMessages.ExamineInfoResponseMessage(
                GetNetEntity(target), 0, newMessage, verbs?.ToList(), centerAtCursor
            );

            RaiseNetworkEvent(ev, session.Channel);
        }

        private void ExamineInfoRequest(ExamineSystemMessages.RequestExamineInfoMessage request, EntitySessionEventArgs eventArgs)
        {
            var player = eventArgs.SenderSession;
            var session = eventArgs.SenderSession;
            var channel = player.Channel;
            var entity = GetEntity(request.NetEntity);

            if (session.AttachedEntity is not {Valid: true} playerEnt
                || !EntityManager.EntityExists(entity))
            {
                RaiseNetworkEvent(new ExamineSystemMessages.ExamineInfoResponseMessage(
                    request.NetEntity, request.Id, _entityNotFoundMessage), channel);
                return;
            }

            if (!CanExamine(playerEnt, entity))
            {
                RaiseNetworkEvent(new ExamineSystemMessages.ExamineInfoResponseMessage(
                    request.NetEntity, request.Id, _entityOutOfRangeMessage, knowTarget: false), channel);
                return;
            }

            SortedSet<Verb>? verbs = null;
            if (request.GetVerbs)
                verbs = _verbSystem.GetLocalVerbs(entity, playerEnt, typeof(ExamineVerb));

            var text = GetExamineText(entity, player.AttachedEntity);
            RaiseNetworkEvent(new ExamineSystemMessages.ExamineInfoResponseMessage(
                request.NetEntity, request.Id, text, verbs?.ToList()), channel);
        }
    }
}

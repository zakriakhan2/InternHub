using InternHub.Data;
using InternHub.Hubs;
using InternHub.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace InternHub.Controllers
{
    [Authorize]
    public class MessagesController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IHubContext<AppHub> _hub;

        public MessagesController(ApplicationDbContext db, IHubContext<AppHub> hub)
        {
            _db = db;
            _hub = hub;
        }

        private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        private bool IsAjax => Request.Headers["X-Requested-With"] == "XMLHttpRequest";

        public async Task<IActionResult> Index(int? conversationId)
        {
            var ordered = await GetVisibleConversationsAsync();

            ViewBag.AllUsers = await _db.Users.Where(u => u.Id != CurrentUserId).ToListAsync();
            ViewBag.SelectedConversationId = conversationId ?? ordered.FirstOrDefault()?.Id;

            return View(ordered);
        }

        [HttpGet]
        public async Task<IActionResult> ConversationListPartial(int? selected)
        {
            var ordered = await GetVisibleConversationsAsync();
            ViewData["SelectedConversationId"] = selected;
            return PartialView("_ConversationList", ordered);
        }

        [HttpGet]
        public async Task<IActionResult> MessagePartial(int messageId)
        {
            var message = await _db.Messages
                .Include(m => m.Sender)
                .Include(m => m.DeletionsFor)
                .FirstOrDefaultAsync(m => m.Id == messageId);

            if (message == null) return NotFound();

            bool isParticipant = await _db.ConversationParticipants
                .AnyAsync(cp => cp.ConversationId == message.ConversationId && cp.UserId == CurrentUserId);
            if (!isParticipant) return Forbid();

            if (message.DeletionsFor.Any(d => d.UserId == CurrentUserId))
                return Content(string.Empty);

            var conversation = await _db.Conversations.FindAsync(message.ConversationId);

            ViewData["ConversationId"] = message.ConversationId;
            ViewData["IsGroup"] = conversation?.IsGroup ?? false;

            return PartialView("_MessageBubble", message);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StartConversation(int otherUserId)
        {
            var existing = await _db.Conversations
                .Where(c => !c.IsGroup)
                .Where(c => c.Participants.Any(p => p.UserId == CurrentUserId) && c.Participants.Any(p => p.UserId == otherUserId))
                .FirstOrDefaultAsync();

            if (existing == null)
            {
                existing = new Conversation { IsGroup = false };
                existing.Participants.Add(new ConversationParticipant { UserId = CurrentUserId });
                existing.Participants.Add(new ConversationParticipant { UserId = otherUserId });
                _db.Conversations.Add(existing);
                await _db.SaveChangesAsync();
            }
            else
            {
                var participants = await _db.ConversationParticipants
                    .Where(cp => cp.ConversationId == existing.Id)
                    .ToListAsync();
                foreach (var p in participants) p.IsHidden = false;
                await _db.SaveChangesAsync();
            }

            await NotifyConversationActivityAsync(existing.Id);

            return RedirectToAction("Index", new { conversationId = existing.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateGroup(string groupName, List<int> memberIds)
        {
            if (!User.IsInRole("Admin") && !User.IsInRole("Supervisor")) return Forbid();

            var conversation = new Conversation { IsGroup = true, Name = groupName };
            conversation.Participants.Add(new ConversationParticipant { UserId = CurrentUserId });

            foreach (var id in (memberIds ?? new List<int>()).Distinct().Where(id => id != CurrentUserId))
                conversation.Participants.Add(new ConversationParticipant { UserId = id });

            _db.Conversations.Add(conversation);
            await _db.SaveChangesAsync();

            await NotifyConversationActivityAsync(conversation.Id);

            return RedirectToAction("Index", new { conversationId = conversation.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Send(int conversationId, string body)
        {
            var participant = await _db.ConversationParticipants
                .FirstOrDefaultAsync(cp => cp.ConversationId == conversationId && cp.UserId == CurrentUserId);

            if (participant == null) return Forbid();

            if (string.IsNullOrWhiteSpace(body))
                return IsAjax ? BadRequest() : RedirectToAction("Index", new { conversationId });

            var message = new Message { ConversationId = conversationId, SenderId = CurrentUserId, Body = body };
            _db.Messages.Add(message);

            var hiddenParticipants = await _db.ConversationParticipants
                .Where(cp => cp.ConversationId == conversationId && cp.IsHidden)
                .ToListAsync();
            foreach (var p in hiddenParticipants) p.IsHidden = false;

            await _db.SaveChangesAsync();
            await BroadcastMessageAsync(conversationId, message.Id);

            return IsAjax ? Ok(new { messageId = message.Id }) : RedirectToAction("Index", new { conversationId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditMessage(int messageId, string body, int conversationId)
        {
            var message = await _db.Messages.FindAsync(messageId);
            if (message == null || message.SenderId != CurrentUserId || message.IsDeleted) return Forbid();

            if (string.IsNullOrWhiteSpace(body))
                return IsAjax ? BadRequest() : RedirectToAction("Index", new { conversationId });

            message.Body = body;
            message.IsEdited = true;
            await _db.SaveChangesAsync();
            await BroadcastMessageAsync(conversationId, message.Id);

            return IsAjax ? Ok() : RedirectToAction("Index", new { conversationId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteMessageForEveryone(int messageId, int conversationId)
        {
            var message = await _db.Messages.FindAsync(messageId);
            if (message == null || message.SenderId != CurrentUserId) return Forbid();

            message.IsDeleted = true;
            message.Body = string.Empty;
            await _db.SaveChangesAsync();
            await BroadcastMessageAsync(conversationId, message.Id);

            return IsAjax ? Ok() : RedirectToAction("Index", new { conversationId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteMessageForMe(int messageId, int conversationId)
        {
            bool isParticipant = await _db.ConversationParticipants
                .AnyAsync(cp => cp.ConversationId == conversationId && cp.UserId == CurrentUserId);
            if (!isParticipant) return Forbid();

            bool alreadyHidden = await _db.MessageDeletions
                .AnyAsync(d => d.MessageId == messageId && d.UserId == CurrentUserId);

            if (!alreadyHidden)
            {
                _db.MessageDeletions.Add(new MessageDeletion { MessageId = messageId, UserId = CurrentUserId });
                await _db.SaveChangesAsync();
            }

            return RedirectToAction("Index", new { conversationId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForwardMessage(int messageId, int targetConversationId)
        {
            var message = await _db.Messages.FindAsync(messageId);
            if (message == null || message.IsDeleted) return Forbid();

            bool sourceParticipant = await _db.ConversationParticipants
                .AnyAsync(cp => cp.ConversationId == message.ConversationId && cp.UserId == CurrentUserId);
            bool targetParticipant = await _db.ConversationParticipants
                .AnyAsync(cp => cp.ConversationId == targetConversationId && cp.UserId == CurrentUserId);

            if (!sourceParticipant || !targetParticipant) return Forbid();

            var forwarded = new Message
            {
                ConversationId = targetConversationId,
                SenderId = CurrentUserId,
                Body = message.Body
            };
            _db.Messages.Add(forwarded);

            var hiddenParticipants = await _db.ConversationParticipants
                .Where(cp => cp.ConversationId == targetConversationId && cp.IsHidden)
                .ToListAsync();
            foreach (var p in hiddenParticipants) p.IsHidden = false;

            await _db.SaveChangesAsync();
            await BroadcastMessageAsync(targetConversationId, forwarded.Id);

            return RedirectToAction("Index", new { conversationId = targetConversationId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConversationForMe(int conversationId)
        {
            var participant = await _db.ConversationParticipants
                .FirstOrDefaultAsync(cp => cp.ConversationId == conversationId && cp.UserId == CurrentUserId);
            if (participant == null) return Forbid();

            participant.IsHidden = true;
            await _db.SaveChangesAsync();

            return RedirectToAction("Index");
        }

        private async Task<List<Conversation>> GetVisibleConversationsAsync()
        {
            var conversationIds = await _db.ConversationParticipants
                .Where(cp => cp.UserId == CurrentUserId && !cp.IsHidden)
                .Select(cp => cp.ConversationId)
                .ToListAsync();

            var conversations = await _db.Conversations
                .Where(c => conversationIds.Contains(c.Id))
                .Include(c => c.Participants).ThenInclude(p => p.User)
                .Include(c => c.Messages).ThenInclude(m => m.Sender)
                .Include(c => c.Messages).ThenInclude(m => m.DeletionsFor)
                .ToListAsync();

            foreach (var c in conversations)
            {
                c.Messages = c.Messages
                    .Where(m => !m.DeletionsFor.Any(d => d.UserId == CurrentUserId))
                    .OrderBy(m => m.SentAt)
                    .ToList();
            }

            return conversations
                .OrderByDescending(c => c.Messages.Any() ? c.Messages.Max(m => m.SentAt) : c.CreatedAt)
                .ToList();
        }

        private async Task NotifyConversationActivityAsync(int conversationId)
        {
            var participantIds = await _db.ConversationParticipants
                .Where(cp => cp.ConversationId == conversationId)
                .Select(cp => cp.UserId)
                .ToListAsync();

            foreach (var uid in participantIds)
            {
                await _hub.Clients.Group(AppHub.UserGroup(uid))
                    .SendAsync("ConversationActivity", new { conversationId });
            }
        }

        private async Task BroadcastMessageAsync(int conversationId, int messageId)
        {
            var participantIds = await _db.ConversationParticipants
                .Where(cp => cp.ConversationId == conversationId)
                .Select(cp => cp.UserId)
                .ToListAsync();

            foreach (var uid in participantIds)
            {
                await _hub.Clients.Group(AppHub.UserGroup(uid))
                    .SendAsync("ReceiveMessage", new { conversationId, messageId });
            }

            await NotifyConversationActivityAsync(conversationId);
        }
    }
}
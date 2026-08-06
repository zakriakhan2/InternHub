using InternHub.Data;
using InternHub.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace InternHub.Controllers
{
    [Authorize]
    public class MessagesController : Controller
    {
        private readonly ApplicationDbContext _db;

        public MessagesController(ApplicationDbContext db)
        {
            _db = db;
        }

        private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        public async Task<IActionResult> Index(int? conversationId)
        {
            var conversationIds = await _db.ConversationParticipants
                .Where(cp => cp.UserId == CurrentUserId)
                .Select(cp => cp.ConversationId)
                .ToListAsync();

            var conversations = await _db.Conversations
                .Where(c => conversationIds.Contains(c.Id))
                .Include(c => c.Participants).ThenInclude(p => p.User)
                .Include(c => c.Messages).ThenInclude(m => m.Sender)
                .ToListAsync();

            var ordered = conversations
                .OrderByDescending(c => c.Messages.Any() ? c.Messages.Max(m => m.SentAt) : c.CreatedAt)
                .ToList();

            ViewBag.AllUsers = await _db.Users.Where(u => u.Id != CurrentUserId).ToListAsync();
            ViewBag.SelectedConversationId = conversationId ?? ordered.FirstOrDefault()?.Id;

            return View(ordered);
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

            return RedirectToAction("Index", new { conversationId = conversation.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Send(int conversationId, string body)
        {
            bool isParticipant = await _db.ConversationParticipants
                .AnyAsync(cp => cp.ConversationId == conversationId && cp.UserId == CurrentUserId);

            if (!isParticipant) return Forbid();
            if (string.IsNullOrWhiteSpace(body)) return RedirectToAction("Index", new { conversationId });

            _db.Messages.Add(new Message { ConversationId = conversationId, SenderId = CurrentUserId, Body = body });
            await _db.SaveChangesAsync();

            return RedirectToAction("Index", new { conversationId });
        }
    }
}
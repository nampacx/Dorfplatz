using System.Linq.Expressions;
using System.Text.Json;
using BulletinBoard.Api.Data;
using BulletinBoard.Api.Data.Entities;
using BulletinBoard.Api.Models;
using BulletinBoard.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;

namespace BulletinBoard.Api.Functions;

public class ChatFunctions
{
    public const string HubName = "chat";
    private readonly AppDbContext _db;
    private readonly CurrentUserService _users;

    public ChatFunctions(AppDbContext db, CurrentUserService users)
    {
        _db = db;
        _users = users;
    }

    /// <summary>
    /// What "unread" means, in one place: sent by the other person, never opened, and not
    /// tombstoned by a GDPR erasure. Composed onto a query rather than repeated, so the
    /// badge, the per-conversation counts and the mark-read update cannot drift apart.
    /// </summary>
    private static Expression<Func<Message, bool>> UnreadFor(Guid userId) =>
        m => m.SenderId != userId && m.ReadAt == null && !m.IsDeleted;

    /// <summary>
    /// Returns SignalR connection info scoped to the caller. SWA injects the principal id
    /// via the x-ms-client-principal-id header, so the connection targets this user only.
    /// </summary>
    [Function("negotiate")]
    public IActionResult Negotiate(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "negotiate")] HttpRequest req,
        [SignalRConnectionInfoInput(HubName = HubName, UserId = "{headers.x-ms-client-principal-id}")] string connectionInfo)
    {
        if (!req.Headers.ContainsKey("x-ms-client-principal-id"))
            return new UnauthorizedResult();
        return new ContentResult { Content = connectionInfo, ContentType = "application/json" };
    }

    [Function("ListConversations")]
    public async Task<IActionResult> ListConversations(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "conversations")] HttpRequest req)
    {
        var me = await _users.GetOrCreateUserAsync(req);
        if (me is null) return new UnauthorizedResult();
        if (!CurrentUserService.IsApproved(me)) return HttpResults.PendingApproval();

        var convos = await _db.Conversations
            .Include(c => c.ParticipantA).Include(c => c.ParticipantB)
            .Where(c => c.ParticipantAId == me.Id || c.ParticipantBId == me.Id)
            .OrderByDescending(c => c.LastMessageAt)
            .ToListAsync();

        // Unread counts in one grouped query rather than one per conversation.
        var ids = convos.Select(c => c.Id).ToList();
        var unread = await _db.Messages
            .Where(UnreadFor(me.Id))
            .Where(m => ids.Contains(m.ConversationId))
            .GroupBy(m => m.ConversationId)
            .Select(g => new { ConversationId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ConversationId, x => x.Count);

        var dtos = convos.Select(c =>
        {
            var other = c.ParticipantAId == me.Id ? c.ParticipantB : c.ParticipantA;
            return new ConversationDto(c.Id, c.ListingType, c.ListingId, other.Id,
                other.DisplayName ?? "Unknown", c.LastMessageAt,
                unread.TryGetValue(c.Id, out var n) ? n : 0);
        });
        return new OkObjectResult(dtos);
    }

    /// <summary>
    /// Total unread messages across every conversation. Kept separate and tiny so the
    /// app shell can show a badge without loading the whole chat page.
    /// The literal route is safe next to conversations/{id:guid}: the guid constraint
    /// stops "unread-count" from matching it.
    /// </summary>
    [Function("UnreadCount")]
    public async Task<IActionResult> UnreadCount(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "conversations/unread-count")] HttpRequest req)
    {
        var me = await _users.GetOrCreateUserAsync(req);
        if (me is null) return new UnauthorizedResult();
        if (!CurrentUserService.IsApproved(me)) return HttpResults.PendingApproval();

        var total = await _db.Messages
            .Where(UnreadFor(me.Id))
            .CountAsync(m => m.Conversation.ParticipantAId == me.Id || m.Conversation.ParticipantBId == me.Id);

        return new OkObjectResult(new UnreadCountDto(total));
    }

    /// <summary>
    /// Mark everything the other person sent in this conversation as read. A separate
    /// POST rather than a side effect of fetching messages, so a GET stays a GET.
    /// </summary>
    [Function("MarkConversationRead")]
    public async Task<IActionResult> MarkRead(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "conversations/{id:guid}/read")] HttpRequest req,
        Guid id)
    {
        var me = await _users.GetOrCreateUserAsync(req);
        if (me is null) return new UnauthorizedResult();
        if (!CurrentUserService.IsApproved(me)) return HttpResults.PendingApproval();

        var convo = await _db.Conversations.FirstOrDefaultAsync(c => c.Id == id);
        if (convo is null) return new NotFoundResult();
        if (!convo.HasParticipant(me.Id)) return HttpResults.Forbidden();

        var now = DateTimeOffset.UtcNow;
        var marked = await _db.Messages
            .Where(UnreadFor(me.Id))
            .Where(m => m.ConversationId == id)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.ReadAt, now));

        return new OkObjectResult(new { marked });
    }

    [Function("ListMessages")]
    public async Task<IActionResult> ListMessages(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "conversations/{id:guid}/messages")] HttpRequest req,
        Guid id)
    {
        var me = await _users.GetOrCreateUserAsync(req);
        if (me is null) return new UnauthorizedResult();
        if (!CurrentUserService.IsApproved(me)) return HttpResults.PendingApproval();

        var convo = await _db.Conversations.FirstOrDefaultAsync(c => c.Id == id);
        if (convo is null) return new NotFoundResult();
        if (!convo.HasParticipant(me.Id)) return HttpResults.Forbidden();

        var messages = await _db.Messages.Include(m => m.Sender)
            .Where(m => m.ConversationId == id)
            .OrderBy(m => m.SentAt)
            .Take(500)
            .ToListAsync();

        var dtos = messages.Select(m => new MessageDto(m.Id, m.ConversationId, m.SenderId,
            m.Sender.DisplayName ?? "Unknown", m.IsDeleted ? "[deleted]" : m.Body, m.SentAt, m.SenderId == me.Id));
        return new OkObjectResult(dtos);
    }

    [Function("StartConversation")]
    public async Task<StartConversationOutput> StartConversation(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "conversations")] HttpRequest req)
    {
        var me = await _users.GetOrCreateUserAsync(req);
        if (me is null) return new StartConversationOutput { HttpResponse = new UnauthorizedResult() };
        if (!CurrentUserService.IsApproved(me)) return new StartConversationOutput { HttpResponse = HttpResults.PendingApproval() };

        var body = await req.ReadJsonAsync<StartConversationRequest>();
        if (body is null || string.IsNullOrWhiteSpace(body.FirstMessage) || body.RecipientId == me.Id)
            return new StartConversationOutput { HttpResponse = new BadRequestObjectResult("Invalid recipient or message.") };

        var recipient = await _db.Users.FirstOrDefaultAsync(u => u.Id == body.RecipientId && u.DeletedAt == null);
        if (recipient is null)
            return new StartConversationOutput { HttpResponse = new NotFoundObjectResult("Recipient not found.") };

        // Reuse an existing conversation between these two for the same listing if present.
        var convo = await _db.Conversations.FirstOrDefaultAsync(c =>
            ((c.ParticipantAId == me.Id && c.ParticipantBId == recipient.Id) ||
             (c.ParticipantAId == recipient.Id && c.ParticipantBId == me.Id)) &&
            c.ListingType == body.ListingType && c.ListingId == body.ListingId);

        if (convo is null)
        {
            convo = new Conversation
            {
                ParticipantAId = me.Id,
                ParticipantBId = recipient.Id,
                ListingType = body.ListingType,
                ListingId = body.ListingId
            };
            _db.Conversations.Add(convo);
        }

        var message = new Message { Conversation = convo, SenderId = me.Id, Body = body.FirstMessage.Trim() };
        _db.Messages.Add(message);
        convo.LastMessageAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        var dto = new MessageDto(message.Id, convo.Id, me.Id, me.DisplayName ?? "You", message.Body, message.SentAt, false);
        return new StartConversationOutput
        {
            SignalRMessage = ToRecipient(recipient.ProviderUserId, dto),
            HttpResponse = new OkObjectResult(new { conversationId = convo.Id })
        };
    }

    [Function("SendMessage")]
    public async Task<SendMessageOutput> SendMessage(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "messages")] HttpRequest req)
    {
        var me = await _users.GetOrCreateUserAsync(req);
        if (me is null) return new SendMessageOutput { HttpResponse = new UnauthorizedResult() };
        if (!CurrentUserService.IsApproved(me)) return new SendMessageOutput { HttpResponse = HttpResults.PendingApproval() };

        var body = await req.ReadJsonAsync<SendMessageRequest>();
        if (body is null || string.IsNullOrWhiteSpace(body.Body))
            return new SendMessageOutput { HttpResponse = new BadRequestObjectResult("Message body required.") };

        var convo = await _db.Conversations
            .Include(c => c.ParticipantA).Include(c => c.ParticipantB)
            .FirstOrDefaultAsync(c => c.Id == body.ConversationId);
        if (convo is null) return new SendMessageOutput { HttpResponse = new NotFoundResult() };
        if (!convo.HasParticipant(me.Id))
            return new SendMessageOutput { HttpResponse = HttpResults.Forbidden() };

        var recipient = convo.ParticipantAId == me.Id ? convo.ParticipantB : convo.ParticipantA;

        var message = new Message { ConversationId = convo.Id, SenderId = me.Id, Body = body.Body.Trim() };
        _db.Messages.Add(message);
        convo.LastMessageAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        var dto = new MessageDto(message.Id, convo.Id, me.Id, me.DisplayName ?? "You", message.Body, message.SentAt, false);
        return new SendMessageOutput
        {
            SignalRMessage = ToRecipient(recipient.ProviderUserId, dto),
            HttpResponse = new OkObjectResult(dto)
        };
    }

    private static SignalRMessageAction ToRecipient(string recipientProviderUserId, MessageDto dto) =>
        new("newMessage")
        {
            UserId = recipientProviderUserId,
            Arguments = new object[] { dto }
        };
}

public class StartConversationOutput
{
    [SignalROutput(HubName = ChatFunctions.HubName)]
    public SignalRMessageAction? SignalRMessage { get; set; }

    [HttpResult]
    public IActionResult HttpResponse { get; set; } = new OkResult();
}

public class SendMessageOutput
{
    [SignalROutput(HubName = ChatFunctions.HubName)]
    public SignalRMessageAction? SignalRMessage { get; set; }

    [HttpResult]
    public IActionResult HttpResponse { get; set; } = new OkResult();
}

using BulletinBoard.Api.Data;

namespace BulletinBoard.Api.Models;

// ---- Events ----
public record CreateEventRequest(string Title, string Description, DateTimeOffset? EventDate, string? Location,
    string? Category);
public record UpdateEventRequest(string Title, string Description, DateTimeOffset? EventDate, string? Location,
    string? Category);
public record RejectEventRequest(string Reason);
public record EventDto(Guid Id, string Title, string Description, DateTimeOffset? EventDate, string? Location,
    string Category, string Status, string? RejectionReason, string AuthorName, DateTimeOffset CreatedAt,
    bool CanEdit);

// ---- Services ----
public record CreateServiceRequest(string Type, string Title, string Description, string? Category);
public record UpdateServiceRequest(string Type, string Title, string Description, string? Category);
public record ServiceDto(Guid Id, string Type, string Title, string Description, string? Category,
    string Status, string AuthorName, Guid AuthorId, DateTimeOffset CreatedAt, bool CanEdit);

// ---- Goods ----
public record CreateGoodsRequest(string Title, string Description, decimal Price, string Currency,
    string Condition, string? Category, string? Location, List<string> BlobNames);

/// <summary>
/// Edit payload. <paramref name="KeepImageIds"/> lists the existing images to retain -
/// anything omitted is deleted - and <paramref name="NewBlobNames"/> adds freshly
/// uploaded ones.
/// </summary>
public record UpdateGoodsRequest(string Title, string Description, decimal Price, string Currency,
    string Condition, string? Category, string? Location,
    List<Guid> KeepImageIds, List<string> NewBlobNames);

public record GoodsImageDto(Guid Id, string Url);
public record GoodsDto(Guid Id, string Title, string Description, decimal Price, string Currency,
    string Condition, string? Category, string? Location, string Status, string AuthorName, Guid AuthorId,
    List<GoodsImageDto> Images, DateTimeOffset CreatedAt, bool CanEdit);

public record UploadRequest(List<UploadFile> Files);
public record UploadFile(string ContentType, long SizeBytes, string Extension);
public record UploadSlot(string BlobName, string UploadUrl);

// ---- Chat ----
public record StartConversationRequest(Guid RecipientId, string? ListingType, Guid? ListingId, string FirstMessage);
public record SendMessageRequest(Guid ConversationId, string Body);
public record ConversationDto(Guid Id, string? ListingType, Guid? ListingId, Guid OtherUserId,
    string OtherUserName, DateTimeOffset LastMessageAt, int UnreadCount);
public record UnreadCountDto(int Total);
public record MessageDto(Guid Id, Guid ConversationId, Guid SenderId, string SenderName, string Body,
    DateTimeOffset SentAt, bool IsMine);

// ---- Admin ----
public record AdminUserDto(Guid Id, string? Email, string? DisplayName, string[] Roles, DateTimeOffset CreatedAt);
public record UpdateRolesRequest(string Role, bool Grant);

// ---- Account / me ----
public record MeDto(Guid Id, string? Email, string? DisplayName, string[] Roles, bool HasConsented);

/// <summary>Only the display name is editable; the email comes from the identity provider.</summary>
public record UpdateProfileRequest(string DisplayName);

namespace BulletinBoard.Api.Data.Entities;

/// <summary>
/// A classifieds ("eBay Kleinanzeigen") listing: description, images and a price.
/// </summary>
public class GoodsPost
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Title { get; set; } = default!;
    public string Description { get; set; } = default!;
    public decimal Price { get; set; }
    public string Currency { get; set; } = "EUR";
    public GoodsCondition Condition { get; set; } = GoodsCondition.Used;
    public string? Category { get; set; }
    public string? Location { get; set; }

    public Guid AuthorId { get; set; }
    public AppUser Author { get; set; } = default!;

    public GoodsStatus Status { get; set; } = GoodsStatus.Active;

    public ICollection<GoodsImage> Images { get; set; } = new List<GoodsImage>();

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DeletedAt { get; set; }
}

public class GoodsImage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GoodsPostId { get; set; }
    public GoodsPost GoodsPost { get; set; } = default!;

    /// <summary>The blob name inside the listing-images container.</summary>
    public string BlobName { get; set; } = default!;
    public int SortOrder { get; set; }
}

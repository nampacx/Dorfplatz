using BulletinBoard.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace BulletinBoard.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<EventPost> Events => Set<EventPost>();
    public DbSet<ServicePost> Services => Set<ServicePost>();
    public DbSet<GoodsPost> Goods => Set<GoodsPost>();
    public DbSet<GoodsImage> GoodsImages => Set<GoodsImage>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<AppUser>(e =>
        {
            e.HasIndex(u => u.ProviderUserId).IsUnique();
            e.Property(u => u.ProviderUserId).HasMaxLength(200);
            e.Property(u => u.Email).HasMaxLength(320);
            e.Property(u => u.DisplayName).HasMaxLength(200);
            // No HasDefaultValue: EF would read the CLR default (Pending) as
            // "unset" and write the database default instead. Same trap the
            // EventPost.Category comment describes. New rows are Pending
            // because the property initialiser says so.
            e.Property(u => u.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(u => u.StatusReason).HasMaxLength(500);
            e.HasIndex(u => u.Status);
        });

        b.Entity<UserRole>(e =>
        {
            e.HasIndex(r => new { r.UserId, r.Role }).IsUnique();
            e.Property(r => r.Role).HasMaxLength(20);
            e.HasOne(r => r.User).WithMany(u => u.Roles)
                .HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<EventPost>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            // No HasDefaultValue here on purpose: EF would treat the CLR default
            // (Verwaltung) as "unset" and silently store Sonstiges instead. Existing
            // rows are backfilled by the migration instead.
            e.Property(x => x.Category).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.ExternalImageUrl).HasMaxLength(1000);
            e.Property(x => x.ImageBlobName).HasMaxLength(300);
            e.Property(x => x.RejectionReason).HasMaxLength(1000);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.Category);
            e.HasOne(x => x.Author).WithMany().HasForeignKey(x => x.AuthorId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<ServicePost>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Category).HasMaxLength(100);
            e.HasIndex(x => new { x.Type, x.Status });
            e.HasOne(x => x.Author).WithMany().HasForeignKey(x => x.AuthorId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<GoodsPost>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.Price).HasPrecision(12, 2);
            e.Property(x => x.Currency).HasMaxLength(3);
            e.Property(x => x.Condition).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => x.Status);
            e.HasMany(x => x.Images).WithOne(i => i.GoodsPost)
                .HasForeignKey(i => i.GoodsPostId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Author).WithMany().HasForeignKey(x => x.AuthorId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<GoodsImage>(e => e.Property(x => x.BlobName).HasMaxLength(300));

        b.Entity<Conversation>(e =>
        {
            e.Property(x => x.ListingType).HasMaxLength(20);
            e.HasIndex(x => new { x.ParticipantAId, x.ParticipantBId });
            e.HasOne(x => x.ParticipantA).WithMany().HasForeignKey(x => x.ParticipantAId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.ParticipantB).WithMany().HasForeignKey(x => x.ParticipantBId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Message>(e =>
        {
            e.Property(x => x.Body).HasMaxLength(4000);
            e.HasIndex(x => new { x.ConversationId, x.SentAt });
            e.HasOne(x => x.Conversation).WithMany(c => c.Messages)
                .HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Sender).WithMany().HasForeignKey(x => x.SenderId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<AuditLog>(e =>
        {
            e.Property(x => x.Action).HasMaxLength(50);
            e.HasIndex(x => x.CreatedAt);
        });
    }
}

using LocalChatbot.Api.Features.Conversations.Models;
using Microsoft.EntityFrameworkCore;

namespace LocalChatbot.Api.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options)
  : DbContext(options)
{
    public DbSet<Conversation> Conversations => Set<Conversation>();

    public DbSet<Message> Messages => Set<Message>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Conversation>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.ExternalChatId)
                .HasMaxLength(100);

            entity.Property(x => x.Title)
                .HasMaxLength(200);

            entity.HasIndex(x => x.ExternalChatId)
                .IsUnique()
                .HasFilter("\"ExternalChatId\" IS NOT NULL");

            entity.HasMany(x => x.Messages)
                .WithOne(x => x.Conversation)
                .HasForeignKey(x => x.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Message>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Role)
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(x => x.Content)
                .HasColumnType("text")
                .IsRequired();

            entity.Property(x => x.ExternalMessageId)
                .HasMaxLength(100);

            entity.HasIndex(x => x.ExternalMessageId)
                .IsUnique()
                .HasFilter("\"ExternalMessageId\" IS NOT NULL");

            entity.HasIndex(x => new
            {
                x.ConversationId,
                x.CreatedAt,
                x.Id
            });
        });
    }
}

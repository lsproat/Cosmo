using Cosmo.Domain.Conversations;
using Microsoft.EntityFrameworkCore;

namespace Cosmo.Infrastructure;

public sealed class CosmoDbContext(DbContextOptions<CosmoDbContext> options) : DbContext(options)
{
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<ConversationMessage> ConversationMessages => Set<ConversationMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var conversations = modelBuilder.Entity<Conversation>();
        conversations.ToTable("Conversations");
        conversations.HasKey(conversation => conversation.Id);
        conversations.Property(conversation => conversation.Id).ValueGeneratedNever();
        conversations.Property(conversation => conversation.CreatedAt).IsRequired();

        conversations.HasMany(conversation => conversation.Messages)
            .WithOne()
            .HasForeignKey("ConversationId")
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
        conversations.Navigation(conversation => conversation.Messages)
            .HasField("_messages")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        var messages = modelBuilder.Entity<ConversationMessage>();
        messages.ToTable("ConversationMessages");
        messages.HasKey(message => message.Id);
        messages.Property(message => message.Id).ValueGeneratedNever();

        // Getter-only properties must be explicitly mapped. EF can populate
        // their backing fields and bind Role/Content to the constructor.
        messages.Property(message => message.Role).IsRequired();
        messages.Property(message => message.Content).IsRequired();
        messages.Property(message => message.CreatedAt).IsRequired();

        // Keep the persistence-only foreign key out of the domain entity.
        messages.Property<Guid>("ConversationId").IsRequired();
        messages.HasIndex("ConversationId", nameof(ConversationMessage.CreatedAt));
    }
}

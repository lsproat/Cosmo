using Cosmo.Application.Abstractions;
using Cosmo.Domain.Conversations;
using Microsoft.EntityFrameworkCore;

namespace Cosmo.Infrastructure;

public sealed class CosmoDbContext(DbContextOptions<CosmoDbContext> options) : DbContext(options), ICosmoDbContext
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
        messages.Property(message => message.Id).ValueGeneratedNever().HasColumnOrder(0);
        messages.Property(message => message.SequenceNumber).IsRequired().HasColumnOrder(1);

        // Getter-only properties must be explicitly mapped. EF can populate
        // their backing fields and bind Role/Content to the constructor.
        messages.Property(message => message.Role).IsRequired().HasColumnOrder(2);
        messages.Property(message => message.Content).IsRequired().HasColumnOrder(3);
        messages.Property(message => message.CreatedAt).IsRequired().HasColumnOrder(4);

        // Keep the persistence-only foreign key out of the domain entity.
        messages.Property<Guid>("ConversationId").IsRequired().HasColumnOrder(5);
        messages.HasIndex("ConversationId", nameof(ConversationMessage.SequenceNumber)).IsUnique();
    }
}

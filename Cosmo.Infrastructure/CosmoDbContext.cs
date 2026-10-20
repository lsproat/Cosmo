using Cosmo.Application.Abstractions;
using Cosmo.Domain.Conversations;
using Cosmo.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Cosmo.Infrastructure;

public sealed class CosmoDbContext(DbContextOptions<CosmoDbContext> options) : DbContext(options), ICosmoDbContext
{
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<ConversationMessage> ConversationMessages => Set<ConversationMessage>();
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Users
        var users = modelBuilder.Entity<User>();

        users.ToTable("Users");
        users.HasKey(user => user.Id);

        users.Property(user => user.Id)
            .UseIdentityAlwaysColumn();

        users.Property(user => user.ExternalIssuer)
            .IsRequired();

        users.Property(user => user.ExternalSubject)
            .IsRequired();

        users.Property(user => user.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        users.HasAlternateKey(user => new
        {
            user.ExternalIssuer,
            user.ExternalSubject
        })
            .HasName("UQ_Users_ExternalIssuer_ExternalSubject");

        // Conversations
        var conversations = modelBuilder.Entity<Conversation>();

        conversations.ToTable("Conversations");
        conversations.HasKey(conversation => conversation.Id);

        conversations.Property(conversation => conversation.Id)
            .UseIdentityAlwaysColumn();

        conversations.Property(conversation => conversation.UserId)
            .IsRequired();

        conversations.Property(conversation => conversation.Title)
            .IsRequired();

        conversations.Property(conversation => conversation.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        conversations.HasOne<User>()
            .WithMany()
            .HasForeignKey(conversation => conversation.UserId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        conversations.HasIndex(conversation => conversation.UserId);

        conversations.HasMany(conversation => conversation.Messages)
            .WithOne()
            .HasForeignKey("ConversationId")
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        conversations.Navigation(conversation => conversation.Messages)
            .HasField("_messages")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // Conversation messages
        var messages = modelBuilder.Entity<ConversationMessage>();

        messages.ToTable("ConversationMessages");
        messages.HasKey(message => message.Id);

        messages.Property(message => message.Id)
            .UseIdentityAlwaysColumn()
            .HasColumnOrder(0);

        // Explicitly map getter-only properties.
        messages.Property(message => message.SequenceNumber)
            .IsRequired()
            .HasColumnOrder(1);

        messages.Property(message => message.Role)
            .IsRequired()
            .HasColumnOrder(2);

        messages.Property(message => message.Content)
            .IsRequired()
            .HasColumnOrder(3);

        messages.Property(message => message.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .HasColumnOrder(4);

        // Shadow foreign key: stored in the database, without a domain property.
        messages.Property<int>("ConversationId")
            .IsRequired()
            .HasColumnOrder(5);

        messages.HasIndex(
                "ConversationId",
                nameof(ConversationMessage.SequenceNumber))
            .IsUnique();
    }
}

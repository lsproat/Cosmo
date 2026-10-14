using Cosmo.Domain.Conversations;
using Cosmo.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Cosmo.IntegrationTests;

public class CosmoDbContextTests
{
    [Fact]
    public void Model_MapsReadOnlyMessagePropertiesAndBackingCollection()
    {
        using var context = CreateContext();
        var conversationType = context.Model.FindEntityType(typeof(Conversation))!;
        var messageType = context.Model.FindEntityType(typeof(ConversationMessage))!;

        var navigation = conversationType.FindNavigation(nameof(Conversation.Messages))!;
        Assert.Equal("_messages", navigation.FieldInfo!.Name);
        Assert.Equal(PropertyAccessMode.Field, navigation.GetPropertyAccessMode());

        foreach (var name in new[] { "Id", "Role", "Content", "CreatedAt" })
        {
            var property = messageType.FindProperty(name);
            Assert.NotNull(property);
            Assert.False(property.IsShadowProperty());
            Assert.NotNull(property.FieldInfo);
        }

        Assert.Equal(ValueGenerated.Never, messageType.FindProperty("Id")!.ValueGenerated);
        Assert.Equal(ValueGenerated.Never, conversationType.FindProperty("Id")!.ValueGenerated);
        var foreignKey = Assert.Single(messageType.GetForeignKeys());
        Assert.True(foreignKey.IsRequired);
        Assert.Equal(DeleteBehavior.Cascade, foreignKey.DeleteBehavior);
        Assert.Equal("ConversationId", Assert.Single(foreignKey.Properties).Name);

        // Validate the relational model without connecting to PostgreSQL.
        var script = context.Database.GenerateCreateScript();
        Assert.Contains("CREATE TABLE \"Conversations\"", script);
        Assert.Contains("CREATE TABLE \"ConversationMessages\"", script);
        Assert.Contains("ON DELETE CASCADE", script);
    }

    [Fact]
    public void Add_TracksExchangeWithExistingIdsAndConversationForeignKey()
    {
        using var context = CreateContext();
        var conversation = new Conversation();
        conversation.AddUserMessage("Hello");
        conversation.AddAssistantMessage("Hi!");
        var originalIds = conversation.Messages.Select(message => message.Id).ToArray();

        context.Conversations.Add(conversation);

        Assert.Equal(EntityState.Added, context.Entry(conversation).State);
        Assert.Equal(originalIds, conversation.Messages.Select(message => message.Id));
        foreach (var message in conversation.Messages)
        {
            var entry = context.Entry(message);
            Assert.Equal(EntityState.Added, entry.State);
            Assert.Equal(conversation.Id, entry.Property<Guid>("ConversationId").CurrentValue);
            Assert.False(entry.Property(message => message.Id).IsTemporary);
        }
    }

    private static CosmoDbContext CreateContext() => new(
        new DbContextOptionsBuilder<CosmoDbContext>()
            .UseNpgsql("Host=localhost;Database=cosmo_model_tests;Username=unused;Password=unused")
            .Options);
}

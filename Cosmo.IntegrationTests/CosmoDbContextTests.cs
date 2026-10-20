using Cosmo.Domain.Conversations;
using Cosmo.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Cosmo.Domain.Users;

namespace Cosmo.IntegrationTests;

public class CosmoDbContextTests
{
    [Fact]
    public void Model_MapsIntegerUserOwnershipAndUniqueExternalIdentity()
    {
        using var context = CreateContext();
        var userType = context.Model.FindEntityType(typeof(User))!;
        var conversationType = context.Model.FindEntityType(typeof(Conversation))!;
        Assert.Equal(typeof(int), userType.FindProperty(nameof(User.Id))!.ClrType);
        Assert.Equal(ValueGenerated.OnAdd, userType.FindProperty(nameof(User.Id))!.ValueGenerated);
        var identityKey = Assert.Single(userType.GetKeys(), key => !key.IsPrimaryKey());
        Assert.Equal(new[] { nameof(User.ExternalIssuer), nameof(User.ExternalSubject) },
            identityKey.Properties.Select(property => property.Name));
        Assert.Equal("UQ_Users_ExternalIssuer_ExternalSubject", identityKey.GetName());
        var ownership = Assert.Single(conversationType.GetForeignKeys());
        Assert.Equal(typeof(User), ownership.PrincipalEntityType.ClrType);
        Assert.Equal(typeof(int), Assert.Single(ownership.Properties).ClrType);
        Assert.True(ownership.IsRequired);
        Assert.Equal(DeleteBehavior.Restrict, ownership.DeleteBehavior);

        var script = context.Database.GenerateCreateScript();
        Assert.Contains("GENERATED ALWAYS AS IDENTITY", script);
        Assert.Contains("ON DELETE RESTRICT", script);
        Assert.DoesNotContain("uuid", script);
    }

    [Fact]
    public void Model_MapsReadOnlyMessagePropertiesAndBackingCollection()
    {
        using var context = CreateContext();
        var conversationType = context.Model.FindEntityType(typeof(Conversation))!;
        var messageType = context.Model.FindEntityType(typeof(ConversationMessage))!;

        var navigation = conversationType.FindNavigation(nameof(Conversation.Messages))!;
        Assert.Equal("_messages", navigation.FieldInfo!.Name);
        Assert.Equal(PropertyAccessMode.Field, navigation.GetPropertyAccessMode());

        foreach (var name in new[] { "Id", "SequenceNumber", "Role", "Content", "CreatedAt" })
        {
            var property = messageType.FindProperty(name);
            Assert.NotNull(property);
            Assert.False(property.IsShadowProperty());
            Assert.NotNull(property.FieldInfo);
        }

        Assert.Equal(ValueGenerated.OnAdd, messageType.FindProperty("Id")!.ValueGenerated);
        Assert.Equal(ValueGenerated.OnAdd, conversationType.FindProperty("Id")!.ValueGenerated);
        var foreignKey = Assert.Single(messageType.GetForeignKeys());
        Assert.True(foreignKey.IsRequired);
        Assert.Equal(DeleteBehavior.Cascade, foreignKey.DeleteBehavior);
        Assert.Equal("ConversationId", Assert.Single(foreignKey.Properties).Name);

        // Validate the relational model without connecting to PostgreSQL.
        var script = context.Database.GenerateCreateScript();
        Assert.Contains("CREATE TABLE \"Conversations\"", script);
        Assert.Contains("CREATE TABLE \"ConversationMessages\"", script);
        Assert.Contains("ON DELETE CASCADE", script);
        var messageTableSql = script[script.IndexOf("CREATE TABLE \"ConversationMessages\"", StringComparison.Ordinal)..];
        var idPosition = messageTableSql.IndexOf("\"Id\" integer", StringComparison.Ordinal);
        var sequencePosition = messageTableSql.IndexOf("\"SequenceNumber\" integer", StringComparison.Ordinal);
        var rolePosition = messageTableSql.IndexOf("\"Role\" integer", StringComparison.Ordinal);
        Assert.True(idPosition >= 0 && idPosition < sequencePosition && sequencePosition < rolePosition);
        var sequenceIndex = Assert.Single(messageType.GetIndexes(), index => index.Properties.Select(property => property.Name)
            .SequenceEqual(new[] { "ConversationId", "SequenceNumber" }));
        Assert.True(sequenceIndex.IsUnique);
    }

    [Fact]
    public void Add_TracksExchangeWithTemporaryGeneratedKeysAndConversationForeignKey()
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
            Assert.Equal(context.Entry(conversation).Property(c => c.Id).CurrentValue,
                entry.Property<int>("ConversationId").CurrentValue);
            Assert.True(entry.Property(message => message.Id).IsTemporary);
        }
    }

    private static CosmoDbContext CreateContext() => new(
        new DbContextOptionsBuilder<CosmoDbContext>()
            .UseNpgsql("Host=localhost;Database=cosmo_model_tests;Username=unused;Password=unused")
            .Options);
}

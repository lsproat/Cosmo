using Cosmo.Domain.Conversations;
using Cosmo.Infrastructure;
using Cosmo.Infrastructure.Conversations;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Cosmo.Application.Abstractions;
using Cosmo.Domain.Users;
using Cosmo.Infrastructure.Users;

namespace Cosmo.IntegrationTests;

// These tests use real PostgreSQL to exercise materialization, SQL translation,
// foreign keys, transactions, and cascading deletes that EF InMemory cannot test.
public class PostgresConversationRepositoryTests
{
    [PostgresIntegrationFact]
    [Trait("Category", "Postgres")]
    public async Task ConcurrentUserResolution_CreatesOneUserAndReturnsTheSameId()
    {
        await using var database = await TestDatabase.CreateAsync();
        var identity = new ExternalUserIdentity("https://test.invalid/", "same-user");
        var tasks = Enumerable.Range(0, 8).Select(async _ =>
        {
            await using var context = database.CreateContext();
            return await new UserResolver(context).GetOrCreateUserIdAsync(identity, CancellationToken.None);
        });
        var ids = await Task.WhenAll(tasks);
        Assert.True(Assert.Single(ids.Distinct()) > 0);
        await using var verification = database.CreateContext();
        Assert.Single(await verification.Users.ToListAsync());
    }

    [PostgresIntegrationFact]
    [Trait("Category", "Postgres")]
    public async Task UserConstraints_RejectDuplicateIdentityAndDeletingConversationOwner()
    {
        await using var database = await TestDatabase.CreateAsync();
        int ownerId;
        await using (var context = database.CreateContext())
        {
            var owner = new User("https://test.invalid/", "owner");
            context.Users.Add(owner);
            await context.SaveChangesAsync();
            ownerId = owner.Id;
            context.Conversations.Add(new Conversation("Private") { UserId = ownerId });
            await context.SaveChangesAsync();
        }

        await using (var context = database.CreateContext())
        {
            context.Users.Add(new User("https://test.invalid/", "owner"));
            var error = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
            var postgres = Assert.IsType<PostgresException>(error.InnerException);
            Assert.Equal(PostgresErrorCodes.UniqueViolation, postgres.SqlState);
            Assert.Equal("UQ_Users_ExternalIssuer_ExternalSubject", postgres.ConstraintName);
        }

        await using (var context = database.CreateContext())
        {
            var error = await Assert.ThrowsAsync<PostgresException>(() =>
                context.Users.Where(user => user.Id == ownerId).ExecuteDeleteAsync());
            Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, error.SqlState);
        }

        await using var verification = database.CreateContext();
        Assert.Single(await verification.Users.ToListAsync());
        Assert.Single(await verification.Conversations.ToListAsync());
    }

    [PostgresIntegrationFact]
    [Trait("Category", "Postgres")]
    public async Task SaveReloadContinueAndDelete_PreservesHistoryAndCascadesMessages()
    {
        await using var database = await TestDatabase.CreateAsync();
        var conversation = new Conversation("First question");
        conversation.AddAssistantMessage("First answer");
        // Supply distinct timestamps explicitly: domain clock precision is not an
        // ordering guarantee, and this test should not depend on machine timing.
        var start = new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);
        await using (var context = database.CreateContext())
        {
            var owner = new Cosmo.Domain.Users.User("https://test.invalid/", "owner");
            context.Users.Add(owner);
            await context.SaveChangesAsync();
            conversation.UserId = owner.Id;
            var repository = new EfConversationRepository(context);
            repository.Add(conversation);
            context.Entry(conversation.Messages[0]).Property(message => message.CreatedAt).CurrentValue = start;
            context.Entry(conversation.Messages[1]).Property(message => message.CreatedAt).CurrentValue = start.AddSeconds(1);
            await repository.SaveChangesAsync(CancellationToken.None);
        }

        await using (var context = database.CreateContext())
        {
            var repository = new EfConversationRepository(context);
            var loaded = Assert.IsType<Conversation>(repository.Get(conversation.Id));
            // PostgreSQL timestamps have microsecond precision; .NET has 100 ns
            // ticks, so a database round trip can discard sub-microsecond ticks.
            Assert.InRange((loaded.CreatedAt - conversation.CreatedAt).Duration(),
                TimeSpan.Zero, TimeSpan.FromMicroseconds(1));
            Assert.Equal(conversation.Messages.Select(message => message.Id), loaded.Messages.Select(message => message.Id));
            Assert.Equal(new[] { "First question", "First answer" }, loaded.Messages.Select(message => message.Content));
            Assert.Equal(new[] { ConversationMessageRole.User, ConversationMessageRole.Assistant }, loaded.Messages.Select(message => message.Role));
            Assert.Equal(new[] { start, start.AddSeconds(1) }, loaded.Messages.Select(message => message.CreatedAt));
            Assert.Equal(new[] { 0, 1 }, loaded.Messages.Select(message => message.SequenceNumber));
            loaded.AddUserMessage("Follow up");
            loaded.AddAssistantMessage("Second answer");
            context.ChangeTracker.DetectChanges();
            context.Entry(loaded.Messages[2]).Property(message => message.CreatedAt).CurrentValue = start.AddSeconds(2);
            context.Entry(loaded.Messages[3]).Property(message => message.CreatedAt).CurrentValue = start.AddSeconds(3);
            await repository.SaveChangesAsync(CancellationToken.None);
        }

        await using (var context = database.CreateContext())
        {
            var loaded = new EfConversationRepository(context).Get(conversation.Id)!;
            Assert.Equal(new[] { "First question", "First answer", "Follow up", "Second answer" }, loaded.Messages.Select(message => message.Content));
            Assert.Equal(new[] { 0, 1, 2, 3 }, loaded.Messages.Select(message => message.SequenceNumber));
            loaded.AddUserMessage("Unsaved failed request");
        }

        await using (var context = database.CreateContext())
        {
            var loaded = new EfConversationRepository(context).Get(conversation.Id)!;
            Assert.Equal(4, loaded.Messages.Count);
        }

        // Delete without loading child entities, so PostgreSQL must cascade.
        await using (var context = database.CreateContext())
        {
            await context.Conversations.Where(item => item.Id == conversation.Id).ExecuteDeleteAsync();
            Assert.Empty(await context.ConversationMessages.ToListAsync());
            Assert.Null(new EfConversationRepository(context).Get(conversation.Id));
        }
    }

    [PostgresIntegrationFact]
    [Trait("Category", "Postgres")]
    public async Task SaveChanges_ForeignKeyFailure_RollsBackEntireExchange()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using (var context = database.CreateContext())
        {
            var owner = new Cosmo.Domain.Users.User("https://test.invalid/", "owner");
            context.Users.Add(owner);
            await context.SaveChangesAsync();
            var conversation = new Conversation("Valid question") { UserId = owner.Id };
            conversation.AddAssistantMessage("Valid answer");
            context.Conversations.Add(conversation);
            var orphan = new ConversationMessage(ConversationMessageRole.User, "Missing conversation");
            context.ConversationMessages.Add(orphan);
            context.Entry(orphan).Property<int>("ConversationId").CurrentValue = int.MaxValue;

            var exception = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
            Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, Assert.IsType<PostgresException>(exception.InnerException).SqlState);
        }

        await using var verification = database.CreateContext();
        Assert.Empty(await verification.Conversations.ToListAsync());
        Assert.Empty(await verification.ConversationMessages.ToListAsync());
    }

    private sealed class TestDatabase(NpgsqlConnection connection, string schema, DbContextOptions<CosmoDbContext> options) : IAsyncDisposable
    {
        public CosmoDbContext CreateContext() => new(options);

        public static async Task<TestDatabase> CreateAsync()
        {
            // Only the explicitly supplied test connection is used, never app settings.
            var connectionString = Environment.GetEnvironmentVariable("COSMO_POSTGRES_TEST_CONNECTION_STRING")!;
            var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            var schema = "cosmo_test_" + Guid.NewGuid().ToString("N");
            var testConnection = new NpgsqlConnectionStringBuilder(connectionString)
            {
                SearchPath = schema,
                Pooling = false
            };
            var options = new DbContextOptionsBuilder<CosmoDbContext>().UseNpgsql(testConnection.ConnectionString).Options;
            var database = new TestDatabase(connection, schema, options);
            try
            {
                // The identifier is generated locally from a fixed prefix and GUID.
                await using var create = new NpgsqlCommand($"CREATE SCHEMA \"{schema}\"", connection);
                await create.ExecuteNonQueryAsync();
                await using var context = database.CreateContext();
                await context.Database.ExecuteSqlRawAsync(context.Database.GenerateCreateScript());
                return database;
            }
            catch
            {
                await database.DisposeAsync();
                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                // Delete only this test's randomly named schema.
                await using var drop = new NpgsqlCommand($"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE", connection);
                await drop.ExecuteNonQueryAsync();
            }
            finally
            {
                await connection.DisposeAsync();
            }
        }
    }
}

public sealed class PostgresIntegrationFactAttribute : FactAttribute
{
    public PostgresIntegrationFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("COSMO_POSTGRES_TEST_CONNECTION_STRING")))
            Skip = "Set COSMO_POSTGRES_TEST_CONNECTION_STRING to run against a PostgreSQL test database.";
    }
}

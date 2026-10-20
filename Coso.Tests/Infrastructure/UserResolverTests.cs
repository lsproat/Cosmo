using Cosmo.Application.Abstractions;
using Cosmo.Infrastructure;
using Cosmo.Infrastructure.Users;
using Microsoft.EntityFrameworkCore;

namespace Cosmo.Tests.Infrastructure;

public class UserResolverTests
{
    [Fact]
    public async Task Resolve_CreatesUserOnceAndReusesIdAcrossRequests()
    {
        var options = CreateOptions();
        var identity = new ExternalUserIdentity("https://issuer.invalid/", "subject");
        int id;
        await using (var db = new CosmoDbContext(options))
        {
            id = await new UserResolver(db).GetOrCreateUserIdAsync(identity, CancellationToken.None);
            Assert.True(id > 0);
        }

        await using var verification = new CosmoDbContext(options);
        Assert.Equal(id, await new UserResolver(verification).GetOrCreateUserIdAsync(identity, CancellationToken.None));
        var stored = Assert.Single(await verification.Users.ToListAsync());
        Assert.Equal(identity.Issuer, stored.ExternalIssuer);
        Assert.Equal(identity.Subject, stored.ExternalSubject);
    }

    [Fact]
    public async Task Resolve_DifferentSubjectsOrIssuersHaveDifferentLocalIds()
    {
        await using var db = new CosmoDbContext(CreateOptions());
        var resolver = new UserResolver(db);
        var first = await resolver.GetOrCreateUserIdAsync(new("https://first.invalid/", "alice"), CancellationToken.None);
        var second = await resolver.GetOrCreateUserIdAsync(new("https://first.invalid/", "bob"), CancellationToken.None);
        var third = await resolver.GetOrCreateUserIdAsync(new("https://second.invalid/", "alice"), CancellationToken.None);
        Assert.Equal(3, new[] { first, second, third }.Distinct().Count());
        Assert.Equal(3, await db.Users.CountAsync());
    }

    [Fact]
    public async Task Resolve_Cancelled_DoesNotCreateUser()
    {
        await using var db = new CosmoDbContext(CreateOptions());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new UserResolver(db)
            .GetOrCreateUserIdAsync(new("https://issuer.invalid/", "subject"), cancellation.Token));
        Assert.Empty(await db.Users.ToListAsync());
    }

    private static DbContextOptions<CosmoDbContext> CreateOptions() =>
        new DbContextOptionsBuilder<CosmoDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
}

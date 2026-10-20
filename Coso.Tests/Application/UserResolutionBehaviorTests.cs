using Cosmo.Application.Abstractions;
using Cosmo.Application.Behaviors;
using Cosmo.Application.Exceptions;

namespace Cosmo.Tests.Application;

public class UserResolutionBehaviorTests
{
    [Fact]
    public async Task UserRequest_OverwritesSuppliedIdBeforeHandlerAndForwardsCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        var identity = new ExternalUserIdentity("https://issuer.invalid/", "subject");
        var current = new CurrentUser(() => identity);
        var resolver = new Resolver((actual, token) =>
        {
            Assert.Equal(identity, actual);
            Assert.Equal(cancellation.Token, token);
            return Task.FromResult(42);
        });
        var request = new UserRequest { UserId = 999 };
        var behavior = new UserResolutionBehavior<UserRequest, string>(current, resolver);

        var result = await behavior.Handle(request, _ =>
        {
            Assert.Equal(42, request.UserId);
            return Task.FromResult("handled");
        }, cancellation.Token);

        Assert.Equal("handled", result);
    }

    [Fact]
    public async Task PublicRequest_DoesNotReadIdentityOrResolveUser()
    {
        var behavior = new UserResolutionBehavior<object, string>(
            new CurrentUser(() => throw new InvalidOperationException("Unexpected identity lookup")),
            new Resolver((_, _) => throw new InvalidOperationException("Unexpected user lookup")));

        Assert.Equal("handled", await behavior.Handle(new object(),
            _ => Task.FromResult("handled"), CancellationToken.None));
    }

    [Fact]
    public async Task MissingIdentity_DoesNotResolveUserOrExecuteHandler()
    {
        var behavior = new UserResolutionBehavior<UserRequest, string>(
            new CurrentUser(() => throw new UnauthenticatedUserException()),
            new Resolver((_, _) => throw new InvalidOperationException("Unexpected user lookup")));

        await Assert.ThrowsAsync<UnauthenticatedUserException>(() => behavior.Handle(new(),
            _ => throw new InvalidOperationException("Unexpected handler invocation"), CancellationToken.None));
    }

    [Fact]
    public async Task ResolutionFailure_DoesNotExecuteHandler()
    {
        var failure = new InvalidOperationException("Database unavailable");
        var behavior = new UserResolutionBehavior<UserRequest, string>(
            new CurrentUser(() => new("https://issuer.invalid/", "subject")),
            new Resolver((_, _) => Task.FromException<int>(failure)));

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => behavior.Handle(new(),
            _ => throw new Exception("Unexpected handler invocation"), CancellationToken.None));
        Assert.Same(failure, actual);
    }

    private sealed class UserRequest : IUserRequest
    {
        public int UserId { get; set; }
    }

    private sealed class CurrentUser(Func<ExternalUserIdentity> read) : ICurrentUser
    {
        public ExternalUserIdentity GetRequiredIdentity() => read();
    }

    private sealed class Resolver(Func<ExternalUserIdentity, CancellationToken, Task<int>> resolve) : IUserResolver
    {
        public Task<int> GetOrCreateUserIdAsync(ExternalUserIdentity identity, CancellationToken cancellationToken)
            => resolve(identity, cancellationToken);
    }
}

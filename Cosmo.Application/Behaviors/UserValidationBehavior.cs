using Cosmo.Application.Abstractions;
using MediatR;

namespace Cosmo.Application.Behaviors;

public sealed class UserResolutionBehavior<TRequest, TResponse>(
    ICurrentUser currentUser,
    IUserResolver userResolver)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (request is IUserRequest userRequest)
        {
            var identity = currentUser.GetRequiredIdentity();

            userRequest.UserId =
                await userResolver.GetOrCreateUserIdAsync(
                    identity,
                    cancellationToken);
        }

        return await next();
    }
}

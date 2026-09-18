using Cosmo.Application.Chat;

namespace Cosmo.Application.Interfaces;

public interface IModelProvider
{
    public Task<SendMessageResult> SendMessageAsync(string message, CancellationToken cancellationToken);
}

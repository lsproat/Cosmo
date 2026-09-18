namespace Cosmo.Application.Abstractions;

public interface IModelProvider
{
    public Task<ModelResponse> SendMessageAsync(string message, CancellationToken cancellationToken);
}

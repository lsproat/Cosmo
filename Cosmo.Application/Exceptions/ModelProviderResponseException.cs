namespace Cosmo.Application.Exceptions;

public sealed class ModelProviderResponseException(
    string message,
    Exception? innerException = null)
    : Exception(message, innerException);

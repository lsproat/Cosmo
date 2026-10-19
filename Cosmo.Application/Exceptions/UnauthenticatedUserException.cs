namespace Cosmo.Application.Exceptions;

public sealed class UnauthenticatedUserException(
    string message = "User is not authenticated.",
    Exception? innerException = null)
    : Exception(message, innerException);

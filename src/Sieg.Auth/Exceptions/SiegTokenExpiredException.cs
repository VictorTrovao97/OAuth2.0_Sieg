using System;

namespace Sieg.Auth.Exceptions;

/// <summary>
/// Exceção lançada quando um token é utilizado após sua expiração.
/// </summary>
public sealed class SiegTokenExpiredException : SiegAuthException
{
    public SiegTokenExpiredException()
    {
    }

    public SiegTokenExpiredException(string message)
        : base(message)
    {
    }

    public SiegTokenExpiredException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}


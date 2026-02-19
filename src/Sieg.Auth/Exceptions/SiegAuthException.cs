using System;

namespace Sieg.Auth.Exceptions;

/// <summary>
/// Exceção base para erros de autenticação com a SIEG.
/// </summary>
public class SiegAuthException : Exception
{
    public SiegAuthException()
    {
    }

    public SiegAuthException(string message)
        : base(message)
    {
    }

    public SiegAuthException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}


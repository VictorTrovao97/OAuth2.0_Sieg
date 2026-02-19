using System;
using System.Net;

namespace Sieg.Auth.Exceptions;

/// <summary>
/// Representa um erro HTTP retornado pelos endpoints OAuth 2.0 da SIEG.
/// </summary>
public sealed class SiegHttpException : SiegAuthException
{
    public HttpStatusCode StatusCode { get; }

    public string? ResponseBody { get; }

    public SiegHttpException(
        HttpStatusCode statusCode,
        string? responseBody,
        string message)
        : base(message)
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }

    public SiegHttpException(
        HttpStatusCode statusCode,
        string? responseBody,
        string message,
        Exception innerException)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }
}


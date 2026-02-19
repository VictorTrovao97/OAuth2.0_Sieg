using System;
using System.Collections.Generic;

namespace Sieg.Auth;

/// <summary>
/// Representa o token definitivo obtido via generate-token,
/// utilizado para consumir as APIs da SIEG.
/// </summary>
public sealed class SiegToken
{
    /// <summary>
    /// Token de acesso definitivo a ser enviado nas requisições à API SIEG.
    /// </summary>
    public string AccessToken { get; }

    /// <summary>
    /// Momento em que o token expira.
    /// </summary>
    public DateTimeOffset ExpiresAt { get; }

    /// <summary>
    /// Token de atualização, quando fornecido pela SIEG.
    /// </summary>
    public string? RefreshToken { get; }

    /// <summary>
    /// Dados adicionais retornados pela SIEG que o integrador queira preservar.
    /// Isso permite acomodar campos específicos sem travar o modelo.
    /// </summary>
    public IReadOnlyDictionary<string, object?> AdditionalData { get; }

    public SiegToken(
        string accessToken,
        DateTimeOffset expiresAt,
        string? refreshToken = null,
        IReadOnlyDictionary<string, object?>? additionalData = null)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new ArgumentException("AccessToken não pode ser vazio.", nameof(accessToken));
        }

        AccessToken = accessToken;
        ExpiresAt = expiresAt;
        RefreshToken = refreshToken;
        AdditionalData = additionalData ?? new Dictionary<string, object?>();
    }

    /// <summary>
    /// Indica se o token está expirado considerando uma folga (clock skew).
    /// </summary>
    /// <param name="now">Instante de comparação (UTC por padrão).</param>
    /// <param name="tolerance">Folga para antecipar a expiração (padrão 1 minuto).</param>
    public bool IsExpired(
        DateTimeOffset? now = null,
        TimeSpan? tolerance = null)
    {
        var current = now ?? DateTimeOffset.UtcNow;
        var skew = tolerance ?? TimeSpan.FromMinutes(1);
        return current >= ExpiresAt - skew;
    }
}



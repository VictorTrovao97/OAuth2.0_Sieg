using System;

namespace Sieg.Auth;

/// <summary>
/// Representa o token temporário retornado na URL de callback
/// após o login/autorização na SIEG.
/// </summary>
public sealed class SiegTemporaryToken
{
    /// <summary>
    /// Valor bruto do token temporário (geralmente obtido de um parâmetro de querystring).
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Instante em que o token foi recebido/criado localmente.
    /// O token em si possui validade curta (por exemplo, 10 minutos).
    /// </summary>
    public DateTimeOffset CreatedAt { get; }

    public SiegTemporaryToken(string value, DateTimeOffset? createdAt = null)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("O valor do token temporário não pode ser vazio.", nameof(value));
        }

        Value = value;
        CreatedAt = createdAt ?? DateTimeOffset.UtcNow;
    }
}


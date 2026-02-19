using System;

namespace Sieg.Auth;

/// <summary>
/// Opções de configuração para autenticação OAuth 2.0 com a SIEG.
/// </summary>
public sealed class SiegOAuthOptions
{
    /// <summary>
    /// ClientId fornecido pela SIEG para identificar o sistema externo.
    /// Utilizado tanto na URL de autorização quanto nos headers das chamadas HTTP.
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Secret Key fornecida pela SIEG.
    /// Enviada no header X-Secret-Key para os endpoints generate-token, refresh e revoke.
    /// </summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>
    /// URL de callback configurada na SIEG (RedirectUri).
    /// Ex.: https://seusistema.com/oauth/callback
    /// </summary>
    public Uri? RedirectUri { get; set; }

    /// <summary>
    /// URL base para a tela de autorização OAuth 2.0.
    /// Normalmente: https://app.sieg.com/AuthorizeAccess.aspx
    /// </summary>
    public Uri BaseAuthorizeUrl { get; set; } =
        new("https://app.sieg.com/AuthorizeAccess.aspx");

    /// <summary>
    /// URL base para os endpoints OAuth 2.0 da API (generate-token, refresh, revoke).
    /// Padrão: https://api.sieg.com/api/v1/oauth/
    /// </summary>
    public Uri BaseApiUrl { get; set; } =
        new("https://api.sieg.com/api/v1/oauth/");

    /// <summary>
    /// Nível de acesso padrão solicitado na URL de autenticação
    /// (read, write ou fullAccess). Pode ser sobrescrito por chamada.
    /// </summary>
    public string? DefaultAccessLevel { get; set; }

    /// <summary>
    /// Janela de antecedência para auto-refresh do token.
    /// Quando estiver a menos que esse tempo de expirar, o SDK tentará um refresh.
    /// </summary>
    public TimeSpan AutoRefreshThreshold { get; set; } = TimeSpan.FromDays(1);
}


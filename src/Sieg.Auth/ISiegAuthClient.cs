using System.Threading;
using System.Threading.Tasks;

namespace Sieg.Auth;

/// <summary>
/// Contrato principal para autenticação com a SIEG via OAuth 2.0.
/// </summary>
public interface ISiegAuthClient
{
    /// <summary>
    /// Monta a URL de autenticação OAuth 2.0 para redirecionar o usuário
    /// à tela de login/autorização da SIEG.
    /// </summary>
    /// <param name="state">
    /// Identificador interno do integrador (para correlação de requisições).
    /// </param>
    /// <param name="accessLevel">
    /// Nível de acesso solicitado (read, write, fullAccess). Se nulo, usa o padrão
    /// configurado em <see cref="SiegOAuthOptions.DefaultAccessLevel"/>.
    /// </param>
    /// <returns>URL completa de autenticação para redirecionar o usuário.</returns>
    string BuildAuthorizationUrl(string state, string? accessLevel = null);

    /// <summary>
    /// Troca um token temporário (recebido via callback) por um token definitivo
    /// utilizando o endpoint generate-token.
    /// </summary>
    /// <param name="temporaryToken">Token temporário recebido na URL de callback.</param>
    /// <param name="ct">Token de cancelamento opcional.</param>
    Task<SiegToken> ExchangeTemporaryTokenAsync(
        string temporaryToken,
        CancellationToken ct = default);

    /// <summary>
    /// Renova um token definitivo utilizando o endpoint refresh.
    /// </summary>
    /// <param name="refreshToken">Refresh token associado ao token definitivo atual.</param>
    /// <param name="ct">Token de cancelamento opcional.</param>
    Task<SiegToken> RefreshTokenAsync(
        string refreshToken,
        CancellationToken ct = default);

    /// <summary>
    /// Revoga um token na SIEG utilizando o endpoint revoke.
    /// </summary>
    /// <param name="accessTokenOrRefreshToken">
    /// Token a ser revogado (access token ou refresh token), conforme a API da SIEG.
    /// </param>
    /// <param name="ct">Token de cancelamento opcional.</param>
    Task RevokeTokenAsync(
        string accessTokenOrRefreshToken,
        CancellationToken ct = default);
}


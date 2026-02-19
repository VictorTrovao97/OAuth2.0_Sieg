using System.Threading;
using System.Threading.Tasks;

namespace Sieg.Auth;

/// <summary>
/// API de alto nível para integrar sistemas emissores com a SIEG.
/// Encapsula todo o fluxo OAuth 2.0 (AuthorizeAccess, generate-token,
/// auto-refresh e revoke) de forma simples.
/// </summary>
public interface ISiegIntegrationClient
{
    /// <summary>
    /// Gera a URL de autorização da SIEG para redirecionar o usuário.
    /// </summary>
    /// <param name="state">Identificador interno do integrador (empresa, usuário, etc.).</param>
    /// <param name="accessLevel">
    /// Nível de acesso solicitado (read, write, fullAccess).
    /// Se omitido, utiliza o valor padrão de <see cref="SiegOAuthOptions.DefaultAccessLevel"/>.
    /// </param>
    string GetAuthorizationUrl(string state, string? accessLevel = null);

    /// <summary>
    /// Conclui o fluxo de autorização utilizando o token temporário recebido no callback
    /// e persiste o token definitivo da SIEG para a conta informada.
    /// </summary>
    /// <param name="accountKey">Identificador da conta no sistema emissor (ex.: CNPJ, ID interno).</param>
    /// <param name="temporaryAccessToken">Valor do parâmetro "accessToken" enviado pela SIEG no callback.</param>
    /// <param name="state">Valor do parâmetro "state" enviado pela SIEG no callback.</param>
    /// <param name="ct">Token de cancelamento opcional.</param>
    Task CompleteAuthorizationAsync(
        string accountKey,
        string temporaryAccessToken,
        string state,
        CancellationToken ct = default);

    /// <summary>
    /// Obtém um access token SIEG válido para a conta informada.
    /// Se o token estiver próximo de expirar, o cliente tenta realizar um refresh automático.
    /// </summary>
    /// <param name="accountKey">Identificador da conta no sistema emissor.</param>
    /// <param name="ct">Token de cancelamento opcional.</param>
    /// <returns>Access token atual para uso nas APIs da SIEG.</returns>
    Task<string> GetValidAccessTokenAsync(
        string accountKey,
        CancellationToken ct = default);

    /// <summary>
    /// Revoga o token associado à conta e remove-o do armazenamento local.
    /// Deve ser utilizado quando o usuário/admin desejar encerrar a integração.
    /// </summary>
    /// <param name="accountKey">Identificador da conta no sistema emissor.</param>
    /// <param name="ct">Token de cancelamento opcional.</param>
    Task RevokeAsync(
        string accountKey,
        CancellationToken ct = default);
}


using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Sieg.Auth.Exceptions;

namespace Sieg.Auth;

/// <summary>
/// Implementação de alto nível de <see cref="ISiegIntegrationClient"/>,
/// que encapsula todo o fluxo OAuth 2.0 com a SIEG, incluindo auto-refresh.
/// </summary>
public sealed class SiegIntegrationClient : ISiegIntegrationClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = null, // Mantém PascalCase conforme a API da SIEG
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly SiegOAuthOptions _options;
    private readonly ISiegTokenStore _tokenStore;
    private readonly ISiegAuthLogger? _logger;

    public SiegIntegrationClient(
        HttpClient httpClient,
        SiegOAuthOptions options,
        ISiegTokenStore tokenStore,
        ISiegAuthLogger? logger = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _tokenStore = tokenStore ?? throw new ArgumentNullException(nameof(tokenStore));
        _logger = logger;

        if (string.IsNullOrWhiteSpace(_options.ClientId))
        {
            throw new ArgumentException("ClientId deve ser configurado em SiegOAuthOptions.", nameof(options));
        }

        if (string.IsNullOrWhiteSpace(_options.SecretKey))
        {
            throw new ArgumentException("SecretKey deve ser configurada em SiegOAuthOptions.", nameof(options));
        }

        if (_options.RedirectUri is null)
        {
            throw new ArgumentException("RedirectUri deve ser configurada em SiegOAuthOptions.", nameof(options));
        }
    }

    /// <inheritdoc />
    public string GetAuthorizationUrl(string state, string? accessLevel = null)
    {
        if (string.IsNullOrWhiteSpace(state))
        {
            throw new ArgumentException("state não pode ser vazio.", nameof(state));
        }

        var level = accessLevel ?? _options.DefaultAccessLevel ?? "read";

        _logger?.LogDebug(
            $"Montando URL de autorização SIEG com state='{state}' e accessLevel='{level}'.");

        var builder = new UriBuilder(_options.BaseAuthorizeUrl);
        var query = new StringBuilder();

        AppendQueryParam(query, "clientId", _options.ClientId);
        AppendQueryParam(query, "state", state);
        AppendQueryParam(query, "accessLevel", level);

        builder.Query = query.ToString().TrimStart('&');

        return builder.Uri.ToString();
    }

    /// <inheritdoc />
    public async Task CompleteAuthorizationAsync(
        string accountKey,
        string temporaryAccessToken,
        string state,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(accountKey))
        {
            throw new ArgumentException("accountKey não pode ser vazio.", nameof(accountKey));
        }

        if (string.IsNullOrWhiteSpace(temporaryAccessToken))
        {
            throw new ArgumentException("temporaryAccessToken não pode ser vazio.", nameof(temporaryAccessToken));
        }

        if (string.IsNullOrWhiteSpace(state))
        {
            throw new ArgumentException("state não pode ser vazio.", nameof(state));
        }

        _logger?.LogInformation($"Concluindo autorização SIEG para conta '{accountKey}'.");

        var endpoint = new Uri(_options.BaseApiUrl, "generate-token");

        var payload = new GenerateTokenRequest
        {
            AccessToken = temporaryAccessToken,
            State = state,
            RedirectUri = _options.RedirectUri!.ToString()
        };

        var apiResponse = await PostJsonAsync<ApiResponse<GenerateTokenData>>(
            endpoint,
            payload,
            ct).ConfigureAwait(false);

        if (!apiResponse.IsSuccess || apiResponse.Data is null ||
            string.IsNullOrWhiteSpace(apiResponse.Data.AccessToken))
        {
            throw new SiegAuthException(
                $"Falha ao gerar token definitivo na SIEG. " +
                $"StatusCode={apiResponse.StatusCode}, Error='{apiResponse.ErrorMessage}'.");
        }

        // Token definitivo com validade de 30 dias a partir de agora.
        var token = new SiegToken(
            apiResponse.Data.AccessToken,
            DateTimeOffset.UtcNow.AddDays(30));

        await _tokenStore.SaveTokenAsync(accountKey, token, ct).ConfigureAwait(false);

        _logger?.LogInformation($"Autorização SIEG concluída e token salvo para conta '{accountKey}'.");
    }

    /// <inheritdoc />
    public async Task<string> GetValidAccessTokenAsync(
        string accountKey,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(accountKey))
        {
            throw new ArgumentException("accountKey não pode ser vazio.", nameof(accountKey));
        }

        var token = await _tokenStore.GetTokenAsync(accountKey, ct).ConfigureAwait(false);

        if (token is null)
        {
            throw new SiegAuthException(
                $"Nenhum token SIEG encontrado para a conta '{accountKey}'. " +
                "Certifique-se de ter concluído o fluxo de autorização.");
        }

        // Se estiver próximo de expirar, tenta auto-refresh.
        if (token.IsExpired(tolerance: _options.AutoRefreshThreshold))
        {
            _logger?.LogInformation(
                $"Token SIEG para conta '{accountKey}' próximo de expirar. Iniciando auto-refresh.");

            await RefreshTokenInternalAsync(token.AccessToken, ct).ConfigureAwait(false);

            // A SIEG mantém o mesmo valor de token e apenas renova a validade no backend.
            var renewed = new SiegToken(
                token.AccessToken,
                DateTimeOffset.UtcNow.AddDays(30));

            await _tokenStore.SaveTokenAsync(accountKey, renewed, ct).ConfigureAwait(false);

            token = renewed;

            _logger?.LogInformation(
                $"Auto-refresh concluído para conta '{accountKey}'. Nova expiração: {renewed.ExpiresAt:O}.");
        }

        return token.AccessToken;
    }

    /// <inheritdoc />
    public async Task RevokeAsync(
        string accountKey,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(accountKey))
        {
            throw new ArgumentException("accountKey não pode ser vazio.", nameof(accountKey));
        }

        var token = await _tokenStore.GetTokenAsync(accountKey, ct).ConfigureAwait(false);
        if (token is null)
        {
            // Nada para revogar.
            _logger?.LogWarning(
                $"Nenhum token SIEG encontrado para a conta '{accountKey}' ao tentar revogar.");
            return;
        }

        _logger?.LogInformation($"Revogando token SIEG para conta '{accountKey}'.");

        var endpoint = new Uri(_options.BaseApiUrl, "revoke");
        var payload = new TokenRequest { Token = token.AccessToken };

        var apiResponse = await PostJsonAsync<ApiResponse<string?>>(
            endpoint,
            payload,
            ct).ConfigureAwait(false);

        if (!apiResponse.IsSuccess)
        {
            throw new SiegAuthException(
                $"Falha ao revogar token na SIEG para conta '{accountKey}'. " +
                $"StatusCode={apiResponse.StatusCode}, Error='{apiResponse.ErrorMessage}'.");
        }

        await _tokenStore.DeleteTokenAsync(accountKey, ct).ConfigureAwait(false);

        _logger?.LogInformation($"Token SIEG revogado e removido do armazenamento para conta '{accountKey}'.");
    }

    #region HTTP helpers

    private async Task<TResponse> PostJsonAsync<TResponse>(
        Uri uri,
        object payload,
        CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(payload, JsonOptions),
                Encoding.UTF8,
                "application/json")
        };

        // Headers exigidos pela SIEG.
        request.Headers.Add("X-Client-Id", _options.ClientId);
        request.Headers.Add("X-Secret-Key", _options.SecretKey);

        _logger?.LogDebug($"Enviando requisição POST para '{uri}'.");

        using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);

        var content = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            _logger?.LogError(
                $"Chamada HTTP para '{uri}' falhou com código {(int)response.StatusCode} ({response.StatusCode}). " +
                $"Corpo: {content}",
                null);

            throw new SiegHttpException(
                response.StatusCode,
                content,
                $"Chamada HTTP para '{uri}' retornou código {(int)response.StatusCode} ({response.StatusCode}).");
        }

        try
        {
            var result = JsonSerializer.Deserialize<TResponse>(content, JsonOptions);
            if (result == null)
            {
                throw new SiegAuthException(
                    $"Não foi possível desserializar a resposta de '{uri}' para o tipo '{typeof(TResponse).Name}'.");
            }

            _logger?.LogDebug(
                $"Resposta HTTP de '{uri}' desserializada com sucesso para '{typeof(TResponse).Name}'.");

            return result;
        }
        catch (JsonException ex)
        {
            _logger?.LogError($"Erro ao desserializar a resposta JSON de '{uri}'.", ex);

            throw new SiegAuthException(
                $"Erro ao desserializar a resposta JSON de '{uri}'.",
                ex);
        }
    }

    private async Task RefreshTokenInternalAsync(
        string accessToken,
        CancellationToken ct)
    {
        var endpoint = new Uri(_options.BaseApiUrl, "refresh");
        var payload = new TokenRequest { Token = accessToken };

        var apiResponse = await PostJsonAsync<ApiResponse<string?>>(
            endpoint,
            payload,
            ct).ConfigureAwait(false);

        if (!apiResponse.IsSuccess)
        {
            throw new SiegAuthException(
                $"Falha ao atualizar token na SIEG. " +
                $"StatusCode={apiResponse.StatusCode}, Error='{apiResponse.ErrorMessage}'.");
        }
    }

    private static void AppendQueryParam(StringBuilder builder, string name, string value)
    {
        if (builder.Length > 0)
        {
            builder.Append('&');
        }

        builder
            .Append(Uri.EscapeDataString(name))
            .Append('=')
            .Append(Uri.EscapeDataString(value));
    }

    #endregion

    #region DTOs

    private sealed class GenerateTokenRequest
    {
        public string AccessToken { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string RedirectUri { get; set; } = string.Empty;
    }

    private sealed class TokenRequest
    {
        public string Token { get; set; } = string.Empty;
    }

    private sealed class ApiResponse<TData>
    {
        public bool IsSuccess { get; set; }
        public string? ErrorMessage { get; set; }
        public int StatusCode { get; set; }
        public TData? Data { get; set; }
        public bool IsFailure { get; set; }
    }

    private sealed class GenerateTokenData
    {
        public int AccessLevel { get; set; }
        public string AccessToken { get; set; } = string.Empty;
        public string TokenType { get; set; } = string.Empty;
    }

    #endregion
}


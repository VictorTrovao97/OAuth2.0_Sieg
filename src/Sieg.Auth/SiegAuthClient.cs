using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Sieg.Auth.Exceptions;

namespace Sieg.Auth;

/// <summary>
/// Implementação padrão de <see cref="ISiegAuthClient"/> que encapsula
/// o fluxo de autenticação OAuth 2.0 com a SIEG.
/// </summary>
public sealed class SiegAuthClient : ISiegAuthClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly SiegOAuthOptions _options;
    private readonly ISiegTokenStore? _tokenStore;
    private readonly ISiegAuthLogger? _logger;

    /// <summary>
    /// Cria uma nova instância de <see cref="SiegAuthClient"/>.
    /// </summary>
    /// <param name="httpClient">
    /// HttpClient a ser utilizado para chamadas HTTP. Deve ser reutilizado (ex.: injetado via DI).
    /// </param>
    /// <param name="options">Opções de configuração da integração com a SIEG.</param>
    /// <param name="tokenStore">Armazenamento opcional de tokens por conta/empresa.</param>
    /// <param name="logger">Logger opcional para diagnóstico.</param>
    public SiegAuthClient(
        HttpClient httpClient,
        SiegOAuthOptions options,
        ISiegTokenStore? tokenStore = null,
        ISiegAuthLogger? logger = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _tokenStore = tokenStore;
        _logger = logger;

        if (string.IsNullOrWhiteSpace(_options.ClientId))
        {
            throw new ArgumentException("ClientId deve ser configurado em SiegOAuthOptions.", nameof(options));
        }

        if (_options.BaseAuthorizeUrl is null)
        {
            throw new ArgumentException("BaseAuthorizeUrl deve ser configurada em SiegOAuthOptions.", nameof(options));
        }
    }

    /// <inheritdoc />
    public string BuildAuthorizationUrl(string state, string? accessLevel = null)
    {
        if (string.IsNullOrWhiteSpace(state))
        {
            throw new ArgumentException("state não pode ser vazio.", nameof(state));
        }

        var level = accessLevel ?? _options.DefaultAccessLevel;

        _logger?.LogDebug(
            $"Montando URL de autenticação SIEG com state='{state}' e accessLevel='{level ?? "(null)"}'.");

        var builder = new UriBuilder(_options.BaseAuthorizeUrl);

        // A documentação SIEG prevê os parâmetros: clientId, state e accessLevel.
        var query = new StringBuilder();
        AppendQueryParam(query, "clientId", _options.ClientId);
        AppendQueryParam(query, "state", state);

        if (!string.IsNullOrWhiteSpace(level))
        {
            AppendQueryParam(query, "accessLevel", level!);
        }

        if (string.IsNullOrEmpty(builder.Query))
        {
            builder.Query = query.ToString().TrimStart('&');
        }
        else
        {
            var existing = builder.Query.TrimStart('?');
            builder.Query = string.IsNullOrEmpty(existing)
                ? query.ToString().TrimStart('&')
                : existing + query;
        }

        return builder.Uri.ToString();
    }

    /// <inheritdoc />
    public async Task<SiegToken> ExchangeTemporaryTokenAsync(
        string temporaryToken,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(temporaryToken))
        {
            throw new ArgumentException("O token temporário não pode ser vazio.", nameof(temporaryToken));
        }

        _logger?.LogInformation("Iniciando troca de token temporário por token definitivo (generate-token).");

        var endpoint = BuildEndpointUri("generate-token");
        var payload = new GenerateTokenRequest
        {
            TemporaryToken = temporaryToken
        };

        var response = await PostJsonAsync<GenerateTokenRequest, SiegTokenResponse>(
            endpoint,
            payload,
            ct).ConfigureAwait(false);

        return ToDomainToken(response);
    }

    /// <inheritdoc />
    public async Task<SiegToken> RefreshTokenAsync(
        string refreshToken,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new ArgumentException("O refresh token não pode ser vazio.", nameof(refreshToken));
        }

        _logger?.LogInformation("Iniciando renovação de token definitivo (refresh).");

        var endpoint = BuildEndpointUri("refresh");
        var payload = new RefreshTokenRequest
        {
            RefreshToken = refreshToken
        };

        var response = await PostJsonAsync<RefreshTokenRequest, SiegTokenResponse>(
            endpoint,
            payload,
            ct).ConfigureAwait(false);

        return ToDomainToken(response);
    }

    /// <inheritdoc />
    public async Task RevokeTokenAsync(
        string accessTokenOrRefreshToken,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(accessTokenOrRefreshToken))
        {
            throw new ArgumentException("O token a ser revogado não pode ser vazio.", nameof(accessTokenOrRefreshToken));
        }

        _logger?.LogInformation("Iniciando revogação de token (revoke).");

        var endpoint = BuildEndpointUri("revoke");
        var payload = new RevokeTokenRequest
        {
            Token = accessTokenOrRefreshToken
        };

        // Geralmente o endpoint de revogação não retorna um corpo relevante.
        await PostJsonAsync<RevokeTokenRequest, object?>(
            endpoint,
            payload,
            ct).ConfigureAwait(false);
    }

    private Uri BuildEndpointUri(string relativePath)
    {
        if (_options.BaseApiUrl is null)
        {
            throw new SiegAuthException(
                "BaseApiUrl deve ser configurada em SiegOAuthOptions para chamar os endpoints OAuth 2.0 da SIEG.");
        }

        // Garante que o caminho seja combinado corretamente (com ou sem / no final/início).
        return new Uri(_options.BaseApiUrl, relativePath);
    }

    private static void AppendQueryParam(StringBuilder builder, string name, string value)
    {
        if (builder.Length == 0)
        {
            builder.Append('&');
        }

        builder
            .Append('&')
            .Append(Uri.EscapeDataString(name))
            .Append('=')
            .Append(Uri.EscapeDataString(value));
    }

    private async Task<TResponse> PostJsonAsync<TRequest, TResponse>(
        Uri uri,
        TRequest payload,
        CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(payload, JsonOptions),
                Encoding.UTF8,
                "application/json")
        };

        _logger?.LogDebug($"Enviando requisição POST para '{uri}'.");

        using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);

        var content = response.Content is null
            ? string.Empty
            : await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            _logger?.LogError(
                $"Chamada HTTP para '{uri}' falhou com código {(int)response.StatusCode} ({response.StatusCode}).",
                null);

            throw new SiegHttpException(
                response.StatusCode,
                content,
                $"Chamada HTTP para '{uri}' retornou código {(int)response.StatusCode} ({response.StatusCode}).");
        }

        if (typeof(TResponse) == typeof(object))
        {
            // Chamadas que não exigem corpo de resposta.
            return default!;
        }

        try
        {
            var result = JsonSerializer.Deserialize<TResponse>(content, JsonOptions);
            if (result == null)
            {
                throw new SiegAuthException(
                    $"Não foi possível desserializar a resposta de '{uri}' para o tipo '{typeof(TResponse).Name}'.");
            }

            _logger?.LogDebug($"Resposta HTTP de '{uri}' desserializada com sucesso para '{typeof(TResponse).Name}'.");

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

    private static SiegToken ToDomainToken(SiegTokenResponse response)
    {
        if (string.IsNullOrWhiteSpace(response.AccessToken))
        {
            throw new SiegAuthException(
                "A resposta da SIEG não contém um access_token válido.");
        }

        // Em muitas implementações OAuth, expires_in é informado em segundos.
        var expiresInSeconds = response.ExpiresIn ?? 0;
        var expiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresInSeconds);

        return new SiegToken(
            response.AccessToken,
            expiresAt,
            response.RefreshToken);
    }

    #region DTOs HTTP internos

    // Os nomes das propriedades podem ser ajustados conforme a documentação real da SIEG.

    private sealed class GenerateTokenRequest
    {
        [JsonPropertyName("temporary_token")]
        public string TemporaryToken { get; set; } = string.Empty;
    }

    private sealed class RefreshTokenRequest
    {
        [JsonPropertyName("refresh_token")]
        public string RefreshToken { get; set; } = string.Empty;
    }

    private sealed class RevokeTokenRequest
    {
        [JsonPropertyName("token")]
        public string Token { get; set; } = string.Empty;
    }

    private sealed class SiegTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("expires_in")]
        public long? ExpiresIn { get; set; }

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; set; }
    }

    #endregion
}


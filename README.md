# Sieg.Auth SDK (OAuth 2.0 SIEG)

Biblioteca .NET para encapsular o fluxo de autenticação OAuth 2.0 da SIEG,
pensada para sistemas emissores de nota fiscal, com auto-refresh de token.

## Estrutura

- `src/Sieg.Auth` — biblioteca principal com:
  - `SiegOAuthOptions` — configuração (ClientId, SecretKey, RedirectUri, URLs, accessLevel padrão, threshold de auto-refresh).
  - `SiegToken` — modelo de token definitivo + expiração.
  - `ISiegIntegrationClient` / `SiegIntegrationClient` — API de alto nível para o emissor.
  - `ISiegTokenStore` / `InMemorySiegTokenStore` — abstração de armazenamento de tokens por conta.
  - Exceções em `Exceptions/` (`SiegAuthException`, `SiegHttpException`, `SiegTokenExpiredException`).

## Fluxo como sistema emissor

### 1. Configuração

```csharp
using Sieg.Auth;

var options = new SiegOAuthOptions
{
    ClientId = "seu-client-id",
    SecretKey = "sua-secret-key",
    RedirectUri = new Uri("https://seu-sistema.com/oauth/callback"),
    DefaultAccessLevel = "read"
    // BaseAuthorizeUrl e BaseApiUrl já vêm com os padrões oficiais da SIEG
};

var httpClient = new HttpClient();
var tokenStore = new InMemorySiegTokenStore();
var logger = (ISiegAuthLogger?)null; // opcional

var sieg = new SiegIntegrationClient(httpClient, options, tokenStore, logger);
```

### 2. Rota para iniciar conexão com a SIEG

```csharp
public IActionResult ConectarSieg(string empresaId)
{
    // Você define como gerar/armazenar o state
    var state = Guid.NewGuid().ToString("N");
    _stateStore.Save(state, empresaId);

    var url = sieg.GetAuthorizationUrl(state, "read");
    return Redirect(url);
}
```

### 3. Callback configurado na SIEG (`RedirectUri`)

A SIEG chamará algo como:

```text
GET /oauth/callback?accessToken=...&state=...
```

No seu código:

```csharp
public async Task<IActionResult> SiegCallback(
    [FromQuery] string accessToken,
    [FromQuery] string state)
{
    var empresaId = _stateStore.GetEmpresaId(state);

    await sieg.CompleteAuthorizationAsync(
        accountKey: empresaId,
        temporaryAccessToken: accessToken,
        state: state);

    return Ok("Integração SIEG concluída para a empresa " + empresaId);
}
```

### 4. Uso diário: obter token válido (com auto-refresh)

```csharp
public async Task<IActionResult> AlgumaAcaoQueChamaSieg(string empresaId)
{
    // O SDK renova o token automaticamente se estiver próximo de expirar
    var accessToken = await sieg.GetValidAccessTokenAsync(empresaId);

    // Use accessToken em Authorization: Bearer <accessToken>
    // nas chamadas às APIs fiscais da SIEG

    return Ok();
}
```

### 5. Encerrar integração (revogar token)

```csharp
public async Task<IActionResult> DesconectarSieg(string empresaId)
{
    await sieg.RevokeAsync(empresaId);
    return Ok();
}
```
*** End Patch`}/>

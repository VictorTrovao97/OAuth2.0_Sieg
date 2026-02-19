## Sieg.Auth SDK (OAuth 2.0 SIEG)

Biblioteca .NET para encapsular o fluxo de autenticação OAuth 2.0 da SIEG,
pensada para sistemas emissores de nota fiscal, com auto-refresh de token.

### Instalação

- **Pacote NuGet**: `Sieg.Auth`  
- **NuGet.org**: https://www.nuget.org/packages/Sieg.Auth

- **Via CLI .NET**

```bash
dotnet add package Sieg.Auth
```

- **Via Package Manager (Visual Studio)**

```powershell
Install-Package Sieg.Auth
```

### Estrutura do repositório

- `src/Sieg.Auth` — biblioteca principal com:
  - `SiegOAuthOptions` — configuração (ClientId, SecretKey, RedirectUri, URLs, accessLevel padrão, threshold de auto-refresh).
  - `SiegToken` — modelo de token definitivo + expiração.
  - `ISiegIntegrationClient` / `SiegIntegrationClient` — API de alto nível para o emissor.
  - `ISiegTokenStore` / `InMemorySiegTokenStore` — abstração de armazenamento de tokens por conta.
  - Exceções em `Exceptions/` (`SiegAuthException`, `SiegHttpException`, `SiegTokenExpiredException`).
- `samples/Sieg.Auth.Samples.Web` — exemplo mínimo em ASP.NET para demonstrar o fluxo completo.

### Guia rápido para emissores

Este guia assume um projeto ASP.NET Core minimal API (`Program.cs`). A ideia é que você tenha endpoints prontos para copiar/colar.

#### 1. Configuração básica (Program.cs)

```csharp
using Sieg.Auth;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(new SiegOAuthOptions
{
    ClientId = "seu-client-id",
    SecretKey = "sua-secret-key",
    RedirectUri = new Uri("https://seu-sistema.com/oauth/callback"),
    DefaultAccessLevel = "read"
});

// PRODUÇÃO: implemente ISiegTokenStore com banco de dados / cache próprio.
builder.Services.AddSingleton<ISiegTokenStore, InMemorySiegTokenStore>();
builder.Services.AddHttpClient<ISiegIntegrationClient, SiegIntegrationClient>();
```

#### 2. Endpoint para iniciar conexão com a SIEG

```csharp
app.MapGet("/sieg/connect", (string empresaId, ISiegIntegrationClient siegClient) =>
{
    var state = Guid.NewGuid().ToString("N");
    // Salve state -> empresaId em algum store próprio.

    var url = siegClient.GetAuthorizationUrl(state, "read");
    return Results.Redirect(url);
});
```

#### 3. Callback configurado na SIEG (`RedirectUri`)

A SIEG chamará algo como:

```text
GET /oauth/callback?accessToken=...&state=...
```

```csharp
app.MapGet("/oauth/callback", async (
    string accessToken,
    string state,
    ISiegIntegrationClient siegClient) =>
{
    var empresaId = /* recuperar a partir do state */;

    await siegClient.CompleteAuthorizationAsync(
        accountKey: empresaId,
        temporaryAccessToken: accessToken,
        state: state);

    return Results.Ok($"Integração SIEG concluída para a empresa {empresaId}.");
});
```

#### 4. Uso diário: obter token válido (com auto-refresh)

```csharp
app.MapGet("/sieg/token", async (string empresaId, ISiegIntegrationClient siegClient) =>
{
    var accessToken = await siegClient.GetValidAccessTokenAsync(empresaId);
    return Results.Ok(new { accessToken });
});
```

#### 5. Encerrar integração (revogar token)

```csharp
app.MapPost("/sieg/revoke", async (string empresaId, ISiegIntegrationClient siegClient) =>
{
    await siegClient.RevokeAsync(empresaId);
    return Results.Ok(new { message = $"Integração SIEG revogada para a empresa {empresaId}" });
});
```


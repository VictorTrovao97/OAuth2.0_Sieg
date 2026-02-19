using Sieg.Auth;

var builder = WebApplication.CreateBuilder(args);

// Configurações básicas do SDK SIEG (substitua pelos seus valores reais).
builder.Services.AddSingleton(new SiegOAuthOptions
{
    ClientId = "SEU_CLIENT_ID",
    SecretKey = "SEU_SECRET_KEY",
    RedirectUri = new Uri("https://localhost:5001/oauth/callback"),
    DefaultAccessLevel = "read"
});

builder.Services.AddSingleton<ISiegTokenStore, InMemorySiegTokenStore>();
builder.Services.AddHttpClient<ISiegIntegrationClient, SiegIntegrationClient>();

var app = builder.Build();

// Endpoint para iniciar o fluxo de autorização com a SIEG.
// GET /sieg/connect?empresaId=123
app.MapGet("/sieg/connect", (string empresaId, ISiegIntegrationClient siegClient) =>
{
    // Em um app real, você poderia salvar state->empresaId em um store.
    var state = Guid.NewGuid().ToString("N");

    var url = siegClient.GetAuthorizationUrl(state, "read");
    return Results.Redirect(url);
});

// Callback configurado na SIEG (RedirectUri).
// GET /oauth/callback?accessToken=...&state=...
app.MapGet("/oauth/callback", async (
    string accessToken,
    string state,
    ISiegIntegrationClient siegClient) =>
{
    // Aqui usamos um accountKey fixo apenas para demonstração.
    // Em um sistema real, recupere empresaId a partir do state.
    var accountKey = "empresa-demo";

    await siegClient.CompleteAuthorizationAsync(
        accountKey,
        accessToken,
        state);

    return Results.Ok(new
    {
        message = "Integração SIEG concluída com sucesso para a conta 'empresa-demo'.",
        state,
        accessToken
    });
});

// Endpoint para testar obtenção de token válido (com auto-refresh).
// GET /sieg/token?empresaId=empresa-demo
app.MapGet("/sieg/token", async (string empresaId, ISiegIntegrationClient siegClient) =>
{
    var token = await siegClient.GetValidAccessTokenAsync(empresaId);
    return Results.Ok(new
    {
        accessToken = token
    });
});

// Endpoint para revogar o token da conta.
// POST /sieg/revoke?empresaId=empresa-demo
app.MapPost("/sieg/revoke", async (string empresaId, ISiegIntegrationClient siegClient) =>
{
    await siegClient.RevokeAsync(empresaId);
    return Results.Ok(new
    {
        message = "Integração SIEG revogada para a conta " + empresaId
    });
});

app.Run();


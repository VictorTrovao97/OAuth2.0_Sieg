using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Sieg.Auth;

/// <summary>
/// Métodos de extensão para facilitar o registro da biblioteca Sieg.Auth no IServiceCollection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adiciona e configura os serviços de autenticação e integração com a API da SIEG.
    /// Registra o HttpClient gerenciado pelo IHttpClientFactory.
    /// </summary>
    public static IServiceCollection AddSiegAuth(
        this IServiceCollection services,
        Action<SiegOAuthOptions> configureOptions)
    {
        if (configureOptions is null)
        {
            throw new ArgumentNullException(nameof(configureOptions));
        }

        // Configura localmente com a sintaxe natural baseada em Options.
        var options = new SiegOAuthOptions();
        configureOptions(options);

        // Disponibiliza as opções por meio do padrão singleton diretamente para injeção via construtor customizado.
        services.AddSingleton(options);

        // Tenta adicionar a Store In-Memory por padrão, apenas se o cliente não registrar uma própria antes.
        services.TryAddSingleton<ISiegTokenStore, InMemorySiegTokenStore>();

        // Registra o HttpClient nomeado/tipado gerando resiliência térmica.
        services.AddHttpClient<ISiegIntegrationClient, SiegIntegrationClient>();

        return services;
    }
}

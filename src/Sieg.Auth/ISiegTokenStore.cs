using System.Threading;
using System.Threading.Tasks;

namespace Sieg.Auth;

/// <summary>
/// Abstração de armazenamento de tokens por conta/empresa.
/// Implementações podem usar memória, banco de dados, cache distribuído etc.
/// </summary>
public interface ISiegTokenStore
{
    /// <summary>
    /// Obtém o token associado a uma conta específica.
    /// </summary>
    /// <param name="accountKey">Chave que identifica a conta (ex.: CNPJ, ID interno).</param>
    /// <param name="ct">Token de cancelamento opcional.</param>
    Task<SiegToken?> GetTokenAsync(
        string accountKey,
        CancellationToken ct = default);

    /// <summary>
    /// Persiste o token associado a uma conta específica.
    /// </summary>
    /// <param name="accountKey">Chave que identifica a conta (ex.: CNPJ, ID interno).</param>
    /// <param name="token">Token a ser salvo.</param>
    /// <param name="ct">Token de cancelamento opcional.</param>
    Task SaveTokenAsync(
        string accountKey,
        SiegToken token,
        CancellationToken ct = default);

    /// <summary>
    /// Remove o token associado a uma conta específica.
    /// </summary>
    /// <param name="accountKey">Chave que identifica a conta (ex.: CNPJ, ID interno).</param>
    /// <param name="ct">Token de cancelamento opcional.</param>
    Task DeleteTokenAsync(
        string accountKey,
        CancellationToken ct = default);
}


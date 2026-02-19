using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Sieg.Auth;

/// <summary>
/// Implementação em memória de <see cref="ISiegTokenStore"/> adequada para
/// cenários simples, testes e aplicações de linha de comando.
/// </summary>
public sealed class InMemorySiegTokenStore : ISiegTokenStore
{
    private readonly ConcurrentDictionary<string, SiegToken> _tokens = new();

    public Task<SiegToken?> GetTokenAsync(
        string accountKey,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        _tokens.TryGetValue(accountKey, out var token);
        return Task.FromResult(token);
    }

    public Task SaveTokenAsync(
        string accountKey,
        SiegToken token,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        _tokens[accountKey] = token;
        return Task.CompletedTask;
    }

    public Task DeleteTokenAsync(
        string accountKey,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        _tokens.TryRemove(accountKey, out _);
        return Task.CompletedTask;
    }
}


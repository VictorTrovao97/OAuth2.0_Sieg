using System;

namespace Sieg.Auth;

/// <summary>
/// Interface de logging opcional utilizada pelo <see cref="SiegAuthClient"/>.
/// 
/// Essa interface é propositalmente simples para não criar dependência
/// direta de nenhum framework de logging. O integrador pode implementar
/// um adaptador para <c>ILogger&lt;T&gt;</c> ou qualquer outra solução.
/// </summary>
public interface ISiegAuthLogger
{
    void LogDebug(string message);

    void LogInformation(string message);

    void LogWarning(string message);

    void LogError(string message, Exception? exception = null);
}


using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;

namespace Fiap.TechChallenge.LambdaAuth.Services;

public class SsmConnectionStringProvider : IConnectionStringProvider
{
    private readonly string? _fallbackConnectionString;
    private readonly string _parameterName;
    private readonly IAmazonSimpleSystemsManagement? _ssmClient;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private string? _cachedConnectionString;

    public SsmConnectionStringProvider(
        string? fallbackConnectionString = null,
        string? parameterName = null,
        IAmazonSimpleSystemsManagement? ssmClient = null)
    {
        _fallbackConnectionString = fallbackConnectionString;
        _parameterName = parameterName
            ?? Environment.GetEnvironmentVariable("SSM_DB_CONNECTION_STRING_PARAM")
            ?? "/techchallenge/prod/db_connection_string";
        _ssmClient = ssmClient;
    }

    public async ValueTask<string> ObterConnectionStringAsync(CancellationToken ct = default)
    {
        // 1. Prioridade: Se houver fallback configurado e NÃO for um placeholder
        if (!string.IsNullOrWhiteSpace(_fallbackConnectionString) &&
            !_fallbackConnectionString.Contains("placeholder", StringComparison.OrdinalIgnoreCase))
        {
            return _fallbackConnectionString;
        }

        // 2. Cache em memória (warm starts na Lambda)
        if (!string.IsNullOrWhiteSpace(_cachedConnectionString))
        {
            return _cachedConnectionString;
        }

        await _semaphore.WaitAsync(ct);
        try
        {
            if (!string.IsNullOrWhiteSpace(_cachedConnectionString))
            {
                return _cachedConnectionString;
            }

            using var client = _ssmClient ?? new AmazonSimpleSystemsManagementClient();
            var response = await client.GetParameterAsync(new GetParameterRequest
            {
                Name = _parameterName,
                WithDecryption = true
            }, ct);

            if (string.IsNullOrWhiteSpace(response.Parameter?.Value))
            {
                throw new InvalidOperationException(
                    $"Parâmetro '{_parameterName}' no AWS SSM Parameter Store está vazio ou não foi encontrado.");
            }

            _cachedConnectionString = response.Parameter.Value;
            return _cachedConnectionString;
        }
        finally
        {
            _semaphore.Release();
        }
    }
}

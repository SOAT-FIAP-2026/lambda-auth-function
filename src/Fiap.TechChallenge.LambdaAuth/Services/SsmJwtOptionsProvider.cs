using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;

namespace Fiap.TechChallenge.LambdaAuth.Services;

public class SsmJwtOptionsProvider : IJwtOptionsProvider
{
    private readonly string? _fallbackSecret;
    private readonly string? _fallbackIssuer;
    private readonly string? _fallbackAudience;
    private readonly int? _fallbackExpiresInSeconds;

    private readonly string _secretParam;
    private readonly string _issuerParam;
    private readonly string _audienceParam;
    private readonly string _expiresInParam;

    private readonly IAmazonSimpleSystemsManagement? _ssmClient;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private JwtOptions? _cachedOptions;

    public SsmJwtOptionsProvider(
        string? fallbackSecret = null,
        string? fallbackIssuer = null,
        string? fallbackAudience = null,
        int? fallbackExpiresInSeconds = null,
        string? basePath = null,
        IAmazonSimpleSystemsManagement? ssmClient = null)
    {
        _fallbackSecret = fallbackSecret;
        _fallbackIssuer = fallbackIssuer;
        _fallbackAudience = fallbackAudience;
        _fallbackExpiresInSeconds = fallbackExpiresInSeconds;

        var basePrefix = basePath
            ?? Environment.GetEnvironmentVariable("SSM_BASE_PATH")
            ?? "/techchallenge/prod";

        _secretParam = Environment.GetEnvironmentVariable("SSM_JWT_SECRET_PARAM") ?? $"{basePrefix}/jwt_secret";
        _issuerParam = Environment.GetEnvironmentVariable("SSM_JWT_ISSUER_PARAM") ?? $"{basePrefix}/jwt_issuer";
        _audienceParam = Environment.GetEnvironmentVariable("SSM_JWT_AUDIENCE_PARAM") ?? $"{basePrefix}/jwt_audience";
        _expiresInParam = Environment.GetEnvironmentVariable("SSM_JWT_EXPIRES_PARAM") ?? $"{basePrefix}/jwt_expires_in_seconds";

        _ssmClient = ssmClient;
    }

    public async ValueTask<JwtOptions> ObterJwtOptionsAsync(CancellationToken ct = default)
    {
        // 1. Prioridade: Se houver fallback configurado e NÃO for um placeholder
        if (!string.IsNullOrWhiteSpace(_fallbackSecret) &&
            !_fallbackSecret.Contains("placeholder", StringComparison.OrdinalIgnoreCase))
        {
            return new JwtOptions(
                _fallbackSecret,
                _fallbackIssuer ?? Environment.GetEnvironmentVariable("JWT_ISSUER") ?? "fiap-tech-challenge",
                _fallbackAudience ?? Environment.GetEnvironmentVariable("JWT_AUDIENCE") ?? "fiap-api",
                _fallbackExpiresInSeconds
                    ?? (int.TryParse(Environment.GetEnvironmentVariable("JWT_EXPIRES_IN_SECONDS"), out var exp) ? exp : 3600));
        }

        // 2. Cache em memória (warm starts na Lambda)
        if (_cachedOptions is not null)
        {
            return _cachedOptions;
        }

        await _semaphore.WaitAsync(ct);
        try
        {
            if (_cachedOptions is not null)
            {
                return _cachedOptions;
            }

            using var client = _ssmClient ?? new AmazonSimpleSystemsManagementClient();
            var response = await client.GetParametersAsync(new GetParametersRequest
            {
                Names = new List<string> { _secretParam, _issuerParam, _audienceParam, _expiresInParam },
                WithDecryption = true
            }, ct);

            var parameters = response.Parameters ?? new List<Parameter>();

            var secret = parameters.FirstOrDefault(p => p.Name == _secretParam)?.Value;
            if (string.IsNullOrWhiteSpace(secret))
            {
                throw new InvalidOperationException(
                    $"Parâmetro '{_secretParam}' no AWS SSM Parameter Store está vazio ou não foi encontrado.");
            }

            var issuer = parameters.FirstOrDefault(p => p.Name == _issuerParam)?.Value
                ?? _fallbackIssuer
                ?? Environment.GetEnvironmentVariable("JWT_ISSUER")
                ?? "fiap-tech-challenge";

            var audience = parameters.FirstOrDefault(p => p.Name == _audienceParam)?.Value
                ?? _fallbackAudience
                ?? Environment.GetEnvironmentVariable("JWT_AUDIENCE")
                ?? "fiap-api";

            var rawExpires = parameters.FirstOrDefault(p => p.Name == _expiresInParam)?.Value;
            var expiresIn = int.TryParse(rawExpires, out var expVal)
                ? expVal
                : _fallbackExpiresInSeconds
                  ?? (int.TryParse(Environment.GetEnvironmentVariable("JWT_EXPIRES_IN_SECONDS"), out var expEnv) ? expEnv : 3600);

            _cachedOptions = new JwtOptions(secret, issuer, audience, expiresIn);
            return _cachedOptions;
        }
        finally
        {
            _semaphore.Release();
        }
    }
}

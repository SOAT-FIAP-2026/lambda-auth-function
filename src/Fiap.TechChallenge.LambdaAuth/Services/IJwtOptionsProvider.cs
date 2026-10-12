namespace Fiap.TechChallenge.LambdaAuth.Services;

public record JwtOptions(string Secret, string Issuer, string Audience, int ExpiresInSeconds);

public interface IJwtOptionsProvider
{
    ValueTask<JwtOptions> ObterJwtOptionsAsync(CancellationToken ct = default);
}

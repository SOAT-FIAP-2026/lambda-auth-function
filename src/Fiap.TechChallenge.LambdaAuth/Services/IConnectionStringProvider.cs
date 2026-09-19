namespace Fiap.TechChallenge.LambdaAuth.Services;

public interface IConnectionStringProvider
{
    ValueTask<string> ObterConnectionStringAsync(CancellationToken ct = default);
}

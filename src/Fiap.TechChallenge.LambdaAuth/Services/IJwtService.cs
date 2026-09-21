namespace Fiap.TechChallenge.LambdaAuth.Services;

public interface IJwtService
{
    ValueTask<(string Token, int ExpiresIn)> GerarTokenAsync(Guid clienteId, string cpf, CancellationToken ct = default);
}

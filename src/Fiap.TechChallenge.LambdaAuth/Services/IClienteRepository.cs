namespace Fiap.TechChallenge.LambdaAuth.Services;

public interface IClienteRepository
{
    Task<Guid> ObterClienteAtivoPorCpfAsync(string cpf, CancellationToken ct = default);
}

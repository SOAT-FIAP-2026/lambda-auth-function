using Dapper;
using Fiap.TechChallenge.LambdaAuth.Exceptions;
using Npgsql;

namespace Fiap.TechChallenge.LambdaAuth.Services;

public class ClienteRepository : IClienteRepository
{
    private readonly IConnectionStringProvider _connectionStringProvider;

    public ClienteRepository(IConnectionStringProvider connectionStringProvider)
    {
        _connectionStringProvider = connectionStringProvider;
    }

    public ClienteRepository(string connectionString)
        : this(new SsmConnectionStringProvider(connectionString))
    { }

    public async Task<Guid> ObterClienteAtivoPorCpfAsync(string cpf, CancellationToken ct = default)
    {
        const string sql = """
            SELECT id AS "Id", apagado_em AS "ApagadoEm"
            FROM cliente
            WHERE cpf_cnpj = @cpf
            LIMIT 1
            """;

        var connectionString = await _connectionStringProvider.ObterConnectionStringAsync(ct);
        await using var conn = new NpgsqlConnection(connectionString);
        var row = await conn.QueryFirstOrDefaultAsync<ClienteRow>(
            new CommandDefinition(sql, new { cpf }, cancellationToken: ct));

        if (row is null)
            throw new ClienteNaoAutorizadoException("Cliente não encontrado.");

        if (row.ApagadoEm.HasValue)
            throw new ClienteNaoAutorizadoException("Cliente inativo.");

        return row.Id;
    }

    private record ClienteRow(Guid Id, DateTime? ApagadoEm);
}

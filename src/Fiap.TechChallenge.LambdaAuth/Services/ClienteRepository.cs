using Dapper;
using Fiap.TechChallenge.LambdaAuth.Exceptions;
using Npgsql;

namespace Fiap.TechChallenge.LambdaAuth.Services;

public class ClienteRepository : IClienteRepository
{
    private readonly string _connectionString;

    public ClienteRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<Guid> ObterClienteAtivoPorCpfAsync(string cpf, CancellationToken ct = default)
    {
        const string sql = """
            SELECT "Id", "ApagadoEm"
            FROM "Clientes"
            WHERE "CpfCnpj" = @cpf
            LIMIT 1
            """;

        await using var conn = new NpgsqlConnection(_connectionString);
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

using System.Net;
using System.Text.Json;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Fiap.TechChallenge.LambdaAuth.Exceptions;
using Fiap.TechChallenge.LambdaAuth.Models;
using Fiap.TechChallenge.LambdaAuth.Services;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace Fiap.TechChallenge.LambdaAuth;

public class Function
{
    private readonly CpfValidatorService _cpfValidator;
    private readonly IClienteRepository  _repository;
    private readonly JwtService          _jwtService;

    public Function(CpfValidatorService cpfValidator, IClienteRepository repository, JwtService jwtService)
    {
        _cpfValidator = cpfValidator;
        _repository   = repository;
        _jwtService   = jwtService;
    }

    public Function() : this(
        new CpfValidatorService(),
        new ClienteRepository(
            Environment.GetEnvironmentVariable("DB_CONNECTION_STRING")
            ?? throw new InvalidOperationException("DB_CONNECTION_STRING não configurada.")),
        new JwtService(
            Environment.GetEnvironmentVariable("JWT_SECRET")
            ?? throw new InvalidOperationException("JWT_SECRET não configurada."),
            Environment.GetEnvironmentVariable("JWT_ISSUER")   ?? "fiap-tech-challenge",
            Environment.GetEnvironmentVariable("JWT_AUDIENCE") ?? "fiap-api",
            int.TryParse(Environment.GetEnvironmentVariable("JWT_EXPIRES_IN_SECONDS"), out var exp) ? exp : 3600))
    { }

    public async Task<APIGatewayProxyResponse> HandleAsync(
        APIGatewayProxyRequest request,
        ILambdaContext context)
    {
        try
        {
            var authRequest    = DeserializarRequest(request.Body);
            var cpfNormalizado = _cpfValidator.ValidarENormalizar(authRequest?.Cpf ?? "");
            var clienteId      = await _repository.ObterClienteAtivoPorCpfAsync(cpfNormalizado);
            var (token, expiresIn) = _jwtService.GerarToken(clienteId, cpfNormalizado);

            return Responder(HttpStatusCode.OK, new AuthResponse(token, "Bearer", expiresIn));
        }
        catch (JsonException)
        {
            return Responder(HttpStatusCode.BadRequest, new { error = "Body inválido." });
        }
        catch (CpfInvalidoException ex)
        {
            return Responder(HttpStatusCode.BadRequest, new { error = ex.Message });
        }
        catch (ClienteNaoAutorizadoException ex)
        {
            return Responder(HttpStatusCode.Unauthorized, new { error = ex.Message });
        }
        catch (Exception ex)
        {
            context.Logger.LogError($"Erro interno: {ex}");
            return Responder(HttpStatusCode.InternalServerError, new { error = "Erro interno." });
        }
    }

    private static AuthRequest? DeserializarRequest(string? body)
    {
        if (string.IsNullOrWhiteSpace(body)) throw new JsonException("Body vazio.");
        return JsonSerializer.Deserialize<AuthRequest>(body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }

    private static APIGatewayProxyResponse Responder(HttpStatusCode status, object body) =>
        new()
        {
            StatusCode = (int)status,
            Headers    = new Dictionary<string, string> { ["Content-Type"] = "application/json" },
            Body       = JsonSerializer.Serialize(body),
        };
}

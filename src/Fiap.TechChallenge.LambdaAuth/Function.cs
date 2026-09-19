using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Fiap.TechChallenge.LambdaAuth.Exceptions;
using Fiap.TechChallenge.LambdaAuth.Models;
using Fiap.TechChallenge.LambdaAuth.Observability;
using Fiap.TechChallenge.LambdaAuth.Services;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace Fiap.TechChallenge.LambdaAuth;

public class Function
{
    public const string CorrelationHeaderName = "X-Correlation-ID";

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
            new SsmConnectionStringProvider(Environment.GetEnvironmentVariable("DB_CONNECTION_STRING"))),
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
        string correlationId = ResolverCorrelationId(request, context);
        var log = new StructuredLogger(context, correlationId);
        long startedAt = Stopwatch.GetTimestamp();

        log.Information("Requisição de autenticação recebida.");

        try
        {
            var authRequest    = DeserializarRequest(request.Body);
            var cpfNormalizado = _cpfValidator.ValidarENormalizar(authRequest?.Cpf ?? "");
            var clienteId      = await _repository.ObterClienteAtivoPorCpfAsync(cpfNormalizado);
            var (token, expiresIn) = _jwtService.GerarToken(clienteId, cpfNormalizado);

            return Responder(
                HttpStatusCode.OK,
                new AuthResponse(token, "Bearer", expiresIn),
                correlationId,
                log,
                startedAt,
                "Token emitido com sucesso.");
        }
        catch (JsonException ex)
        {
            return ResponderFalha(
                HttpStatusCode.BadRequest, "Body inválido.", correlationId, log, startedAt, ex, "invalid_body");
        }
        catch (CpfInvalidoException ex)
        {
            return ResponderFalha(
                HttpStatusCode.BadRequest, ex.Message, correlationId, log, startedAt, ex, "invalid_cpf");
        }
        catch (ClienteNaoAutorizadoException ex)
        {
            return ResponderFalha(
                HttpStatusCode.Unauthorized, ex.Message, correlationId, log, startedAt, ex, "unauthorized_client");
        }
        catch (Exception ex)
        {
            log.Error("Falha interna ao processar a autenticação.", ex, new Dictionary<string, object?>
            {
                ["duration_ms"] = DuracaoMs(startedAt),
                ["status_code"] = (int)HttpStatusCode.InternalServerError,
                ["outcome"]     = "internal_error"
            });

            return Responder(HttpStatusCode.InternalServerError, new { error = "Erro interno." }, correlationId);
        }
    }

    /// <summary>
    /// Reaproveita o X-Correlation-ID enviado pelo API Gateway/cliente para que a
    /// requisição possa ser rastreada na Lambda e na API .NET com o mesmo identificador.
    /// </summary>
    private static string ResolverCorrelationId(APIGatewayProxyRequest request, ILambdaContext? context)
    {
        if (request.Headers is not null)
        {
            string? recebido = request.Headers
                .FirstOrDefault(header => string.Equals(header.Key, CorrelationHeaderName, StringComparison.OrdinalIgnoreCase))
                .Value;

            if (!string.IsNullOrWhiteSpace(recebido) && recebido.Length <= 128)
                return recebido;
        }

        return context?.AwsRequestId is { Length: > 0 } awsRequestId
            ? awsRequestId
            : Guid.NewGuid().ToString("n");
    }

    private static AuthRequest? DeserializarRequest(string? body)
    {
        if (string.IsNullOrWhiteSpace(body)) throw new JsonException("Body vazio.");
        return JsonSerializer.Deserialize<AuthRequest>(body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }

    private static APIGatewayProxyResponse ResponderFalha(
        HttpStatusCode status,
        string mensagem,
        string correlationId,
        StructuredLogger log,
        long startedAt,
        Exception exception,
        string outcome)
    {
        log.Warning(mensagem, new Dictionary<string, object?>
        {
            ["duration_ms"]    = DuracaoMs(startedAt),
            ["status_code"]    = (int)status,
            ["outcome"]        = outcome,
            ["exception_type"] = exception.GetType().Name
        });

        return Responder(status, new { error = mensagem }, correlationId);
    }

    private static APIGatewayProxyResponse Responder(
        HttpStatusCode status,
        object body,
        string correlationId,
        StructuredLogger log,
        long startedAt,
        string mensagem)
    {
        log.Information(mensagem, new Dictionary<string, object?>
        {
            ["duration_ms"] = DuracaoMs(startedAt),
            ["status_code"] = (int)status,
            ["outcome"]     = "success"
        });

        return Responder(status, body, correlationId);
    }

    private static APIGatewayProxyResponse Responder(HttpStatusCode status, object body, string correlationId) =>
        new()
        {
            StatusCode = (int)status,
            Headers    = new Dictionary<string, string>
            {
                ["Content-Type"]        = "application/json",
                [CorrelationHeaderName] = correlationId
            },
            Body = JsonSerializer.Serialize(body),
        };

    private static double DuracaoMs(long startedAt) =>
        Math.Round(Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds, 2);
}

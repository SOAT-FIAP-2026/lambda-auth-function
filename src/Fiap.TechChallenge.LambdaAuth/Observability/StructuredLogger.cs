using System.Text.Json;
using Amazon.Lambda.Core;

namespace Fiap.TechChallenge.LambdaAuth.Observability;

/// <summary>
/// Escreve logs estruturados em JSON (uma linha por evento) no CloudWatch Logs.
/// O formato acompanha o da API .NET (AddJsonConsole) para que Loki/CloudWatch
/// consigam correlacionar a requisição pelo campo correlation_id.
/// Nenhum dado pessoal (CPF, nome, e-mail) é registrado.
/// </summary>
public sealed class StructuredLogger
{
    public const string ServiceName = "techchallenge-lambda-auth";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = false
    };

    private readonly ILambdaLogger? _logger;
    private readonly string _correlationId;
    private readonly string _awsRequestId;

    public StructuredLogger(ILambdaContext? context, string correlationId)
    {
        _logger = context?.Logger;
        _correlationId = correlationId;
        _awsRequestId = context?.AwsRequestId ?? string.Empty;
    }

    public string CorrelationId => _correlationId;

    public void Information(string message, IDictionary<string, object?>? fields = null) =>
        Write("Information", message, fields, exception: null);

    public void Warning(string message, IDictionary<string, object?>? fields = null) =>
        Write("Warning", message, fields, exception: null);

    public void Error(string message, Exception exception, IDictionary<string, object?>? fields = null) =>
        Write("Error", message, fields, exception);

    private void Write(string level, string message, IDictionary<string, object?>? fields, Exception? exception)
    {
        var payload = new Dictionary<string, object?>
        {
            ["Timestamp"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            ["LogLevel"] = level,
            ["Message"] = message,
            ["service"] = ServiceName,
            ["correlation_id"] = _correlationId
        };

        if (!string.IsNullOrEmpty(_awsRequestId))
            payload["aws_request_id"] = _awsRequestId;

        if (fields is not null)
        {
            foreach (var field in fields)
                payload[field.Key] = field.Value;
        }

        if (exception is not null)
        {
            payload["exception_type"] = exception.GetType().FullName;
            payload["exception_message"] = exception.Message;
            payload["exception_stack"] = exception.StackTrace;
        }

        string line = JsonSerializer.Serialize(payload, SerializerOptions);

        if (_logger is not null)
            _logger.LogLine(line);
        else
            Console.WriteLine(line);
    }
}

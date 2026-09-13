using System.Text.Json;
using Amazon.Lambda.Core;
using FluentAssertions;
using Fiap.TechChallenge.LambdaAuth.Observability;
using NSubstitute;

namespace Fiap.TechChallenge.LambdaAuth.Tests.Observability;

public class StructuredLoggerTests
{
    private const string CorrelationId = "corr-123";
    private const string AwsRequestId = "req-456";

    private readonly List<string> _linhas = [];
    private readonly ILambdaContext _context = Substitute.For<ILambdaContext>();

    public StructuredLoggerTests()
    {
        var logger = Substitute.For<ILambdaLogger>();
        logger.When(x => x.LogLine(Arg.Any<string>())).Do(call => _linhas.Add(call.Arg<string>()));

        _context.Logger.Returns(logger);
        _context.AwsRequestId.Returns(AwsRequestId);
    }

    private JsonElement UnicaLinha()
    {
        _linhas.Should().HaveCount(1);
        return JsonDocument.Parse(_linhas[0]).RootElement;
    }

    [Fact]
    public void Information_EscreveCamposObrigatoriosEmJson()
    {
        new StructuredLogger(_context, CorrelationId).Information("Requisição recebida.");

        var log = UnicaLinha();
        log.GetProperty("LogLevel").GetString().Should().Be("Information");
        log.GetProperty("Message").GetString().Should().Be("Requisição recebida.");
        log.GetProperty("service").GetString().Should().Be(StructuredLogger.ServiceName);
        log.GetProperty("correlation_id").GetString().Should().Be(CorrelationId);
        log.GetProperty("aws_request_id").GetString().Should().Be(AwsRequestId);
        log.GetProperty("Timestamp").GetString().Should().EndWith("Z");
    }

    [Fact]
    public void Warning_IncluiCamposAdicionais()
    {
        new StructuredLogger(_context, CorrelationId).Warning("CPF inválido.", new Dictionary<string, object?>
        {
            ["status_code"] = 400,
            ["outcome"] = "invalid_cpf"
        });

        var log = UnicaLinha();
        log.GetProperty("LogLevel").GetString().Should().Be("Warning");
        log.GetProperty("status_code").GetInt32().Should().Be(400);
        log.GetProperty("outcome").GetString().Should().Be("invalid_cpf");
    }

    [Fact]
    public void Error_IncluiDadosDaExcecao()
    {
        var excecao = new InvalidOperationException("db down");

        new StructuredLogger(_context, CorrelationId).Error("Falha interna.", excecao);

        var log = UnicaLinha();
        log.GetProperty("LogLevel").GetString().Should().Be("Error");
        log.GetProperty("exception_type").GetString().Should().Be(typeof(InvalidOperationException).FullName);
        log.GetProperty("exception_message").GetString().Should().Be("db down");
    }

    [Fact]
    public void Logger_SemContexto_NaoLancaExcecao()
    {
        var log = new StructuredLogger(null, CorrelationId);

        log.CorrelationId.Should().Be(CorrelationId);
        log.Invoking(x => x.Information("sem contexto")).Should().NotThrow();
    }

    [Fact]
    public void Logger_SemAwsRequestId_OmiteOCampo()
    {
        var contextoSemRequestId = Substitute.For<ILambdaContext>();
        var logger = Substitute.For<ILambdaLogger>();
        logger.When(x => x.LogLine(Arg.Any<string>())).Do(call => _linhas.Add(call.Arg<string>()));
        contextoSemRequestId.Logger.Returns(logger);
        contextoSemRequestId.AwsRequestId.Returns(string.Empty);

        new StructuredLogger(contextoSemRequestId, CorrelationId).Information("sem request id");

        UnicaLinha().TryGetProperty("aws_request_id", out _).Should().BeFalse();
    }
}

using System.Net;
using System.Text.Json;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using FluentAssertions;
using NSubstitute;
using Fiap.TechChallenge.LambdaAuth.Models;
using Fiap.TechChallenge.LambdaAuth.Services;

namespace Fiap.TechChallenge.LambdaAuth.Tests.Observability;

public class CorrelationTests
{
    private readonly CpfValidatorService _cpfValidator = new();
    private readonly IClienteRepository  _repo         = Substitute.For<IClienteRepository>();
    private readonly JwtService          _jwt          = new(
        "super-secret-key-com-32-caracteres!", "fiap-tech-challenge", "fiap-api", 3600);
    private readonly ILambdaContext _ctx = Substitute.For<ILambdaContext>();

    private Function CriarFunction() => new(_cpfValidator, _repo, _jwt);

    [Fact]
    public async Task Handler_ComCorrelationIdNoHeader_DevolveOMesmoValor()
    {
        _repo.ObterClienteAtivoPorCpfAsync("52998224725", Arg.Any<CancellationToken>())
             .Returns(Guid.NewGuid());

        var request = new APIGatewayProxyRequest
        {
            Body    = JsonSerializer.Serialize(new AuthRequest("529.982.247-25")),
            Headers = new Dictionary<string, string> { ["x-correlation-id"] = "abc-123" }
        };

        var response = await CriarFunction().HandleAsync(request, _ctx);

        response.StatusCode.Should().Be((int)HttpStatusCode.OK);
        response.Headers.Should().ContainKey(Function.CorrelationHeaderName)
                .WhoseValue.Should().Be("abc-123");
    }

    [Fact]
    public async Task Handler_SemCorrelationId_UsaAwsRequestId()
    {
        _ctx.AwsRequestId.Returns("req-999");
        _repo.ObterClienteAtivoPorCpfAsync("52998224725", Arg.Any<CancellationToken>())
             .Returns(Guid.NewGuid());

        var request = new APIGatewayProxyRequest
        {
            Body = JsonSerializer.Serialize(new AuthRequest("529.982.247-25"))
        };

        var response = await CriarFunction().HandleAsync(request, _ctx);

        response.Headers[Function.CorrelationHeaderName].Should().Be("req-999");
    }

    [Fact]
    public async Task Handler_ErroDeValidacao_MantemCorrelationIdNaResposta()
    {
        var request = new APIGatewayProxyRequest
        {
            Body    = "{\"cpf\":\"00000000000\"}",
            Headers = new Dictionary<string, string> { ["X-Correlation-ID"] = "trace-erro" }
        };

        var response = await CriarFunction().HandleAsync(request, _ctx);

        response.StatusCode.Should().Be((int)HttpStatusCode.BadRequest);
        response.Headers[Function.CorrelationHeaderName].Should().Be("trace-erro");
    }
}

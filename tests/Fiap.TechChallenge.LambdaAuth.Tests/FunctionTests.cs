using System.Net;
using System.Text.Json;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Fiap.TechChallenge.LambdaAuth;
using Fiap.TechChallenge.LambdaAuth.Exceptions;
using Fiap.TechChallenge.LambdaAuth.Models;
using Fiap.TechChallenge.LambdaAuth.Services;

namespace Fiap.TechChallenge.LambdaAuth.Tests;

public class FunctionTests
{
    private readonly CpfValidatorService _cpfValidator = new();
    private readonly IClienteRepository  _repo         = Substitute.For<IClienteRepository>();
    private readonly JwtService          _jwt          = new(
        "super-secret-key-com-32-caracteres!", "fiap-tech-challenge", "fiap-api", 3600);
    private readonly ILambdaContext _ctx = Substitute.For<ILambdaContext>();

    private Function CriarFunction() => new(_cpfValidator, _repo, _jwt);

    private static APIGatewayProxyRequest CriarRequest(string body)
        => new() { Body = body };

    [Fact]
    public async Task Handler_CpfValidoClienteAtivo_Retorna200ComToken()
    {
        var clienteId = Guid.NewGuid();
        _repo.ObterClienteAtivoPorCpfAsync("52998224725", Arg.Any<CancellationToken>())
             .Returns(clienteId);

        var request = CriarRequest(JsonSerializer.Serialize(new AuthRequest("529.982.247-25")));
        var response = await CriarFunction().HandleAsync(request, _ctx);

        response.StatusCode.Should().Be((int)HttpStatusCode.OK);
        var body = JsonSerializer.Deserialize<AuthResponse>(response.Body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        body!.TokenType.Should().Be("Bearer");
        body.ExpiresIn.Should().Be(3600);
        body.AccessToken.Should().NotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData("")]
    [InlineData("{}")]
    [InlineData("{\"cpf\":\"00000000000\"}")]
    [InlineData("{\"cpf\":\"529.982.247-26\"}")]
    public async Task Handler_CpfInvalido_Retorna400(string body)
    {
        var response = await CriarFunction().HandleAsync(CriarRequest(body), _ctx);
        response.StatusCode.Should().Be((int)HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Handler_ClienteNaoEncontrado_Retorna401()
    {
        _repo.ObterClienteAtivoPorCpfAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
             .ThrowsAsync(new ClienteNaoAutorizadoException("não encontrado"));

        var request = CriarRequest(JsonSerializer.Serialize(new AuthRequest("529.982.247-25")));
        var response = await CriarFunction().HandleAsync(request, _ctx);

        response.StatusCode.Should().Be((int)HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Handler_ErroInterno_Retorna500()
    {
        _repo.ObterClienteAtivoPorCpfAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
             .ThrowsAsync(new Exception("db down"));

        var request = CriarRequest(JsonSerializer.Serialize(new AuthRequest("529.982.247-25")));
        var response = await CriarFunction().HandleAsync(request, _ctx);

        response.StatusCode.Should().Be((int)HttpStatusCode.InternalServerError);
    }
}

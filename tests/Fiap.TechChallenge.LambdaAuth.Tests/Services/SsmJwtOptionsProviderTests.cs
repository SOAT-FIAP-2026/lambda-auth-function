using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;
using Fiap.TechChallenge.LambdaAuth.Services;
using FluentAssertions;
using NSubstitute;

namespace Fiap.TechChallenge.LambdaAuth.Tests.Services;

public class SsmJwtOptionsProviderTests
{
    private readonly IAmazonSimpleSystemsManagement _ssmClient = Substitute.For<IAmazonSimpleSystemsManagement>();

    [Fact]
    public async Task ObterJwtOptionsAsync_FallbackValido_RetornaDiretamenteSemChamarSsm()
    {
        // Arrange
        const string secretValido = "uma-chave-secreta-de-32-caracteres!";
        var provider = new SsmJwtOptionsProvider(
            fallbackSecret: secretValido,
            fallbackIssuer: "meu-issuer",
            fallbackAudience: "minha-audience",
            fallbackExpiresInSeconds: 7200,
            ssmClient: _ssmClient);

        // Act
        var resultado = await provider.ObterJwtOptionsAsync();

        // Assert
        resultado.Secret.Should().Be(secretValido);
        resultado.Issuer.Should().Be("meu-issuer");
        resultado.Audience.Should().Be("minha-audience");
        resultado.ExpiresInSeconds.Should().Be(7200);

        await _ssmClient.DidNotReceiveWithAnyArgs().GetParametersAsync(Arg.Any<GetParametersRequest>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("placeholder")]
    [InlineData("PLACEHOLDER_SECRET_KEY_MOCK")]
    public async Task ObterJwtOptionsAsync_FallbackNuloOuPlaceholder_ConsultaSsm(string? fallbackSecret)
    {
        // Arrange
        const string basePath = "/techchallenge/test";
        const string secretParam = $"{basePath}/jwt_secret";
        const string issuerParam = $"{basePath}/jwt_issuer";
        const string audienceParam = $"{basePath}/jwt_audience";
        const string expiresParam = $"{basePath}/jwt_expires_in_seconds";

        _ssmClient.GetParametersAsync(
            Arg.Is<GetParametersRequest>(r => r.WithDecryption == true && r.Names.Contains(secretParam)),
            Arg.Any<CancellationToken>())
            .Returns(new GetParametersResponse
            {
                Parameters = new List<Parameter>
                {
                    new() { Name = secretParam, Value = "chave-super-secreta-de-32-chars-ok" },
                    new() { Name = issuerParam, Value = "TechChallenge" },
                    new() { Name = audienceParam, Value = "techchallenge.com.br" },
                    new() { Name = expiresParam, Value = "1800" },
                }
            });

        var provider = new SsmJwtOptionsProvider(
            fallbackSecret: fallbackSecret,
            basePath: basePath,
            ssmClient: _ssmClient);

        // Act
        var resultado = await provider.ObterJwtOptionsAsync();

        // Assert
        resultado.Secret.Should().Be("chave-super-secreta-de-32-chars-ok");
        resultado.Issuer.Should().Be("TechChallenge");
        resultado.Audience.Should().Be("techchallenge.com.br");
        resultado.ExpiresInSeconds.Should().Be(1800);

        await _ssmClient.Received(1).GetParametersAsync(
            Arg.Is<GetParametersRequest>(r => r.WithDecryption == true && r.Names.Contains(secretParam)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ObterJwtOptionsAsync_MultiplasChamadas_UtilizaCacheEmMemoria()
    {
        // Arrange
        const string basePath = "/techchallenge/test";
        const string secretParam = $"{basePath}/jwt_secret";

        _ssmClient.GetParametersAsync(
            Arg.Any<GetParametersRequest>(),
            Arg.Any<CancellationToken>())
            .Returns(new GetParametersResponse
            {
                Parameters = new List<Parameter>
                {
                    new() { Name = secretParam, Value = "chave-super-secreta-de-32-chars-ok" }
                }
            });

        var provider = new SsmJwtOptionsProvider(fallbackSecret: null, basePath: basePath, ssmClient: _ssmClient);

        // Act
        var res1 = await provider.ObterJwtOptionsAsync();
        var res2 = await provider.ObterJwtOptionsAsync();

        // Assert
        res1.Secret.Should().Be("chave-super-secreta-de-32-chars-ok");
        res2.Secret.Should().Be("chave-super-secreta-de-32-chars-ok");
        await _ssmClient.Received(1).GetParametersAsync(Arg.Any<GetParametersRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ObterJwtOptionsAsync_SsmNaoRetornaSecret_LancaInvalidOperationException()
    {
        // Arrange
        const string basePath = "/techchallenge/test";

        _ssmClient.GetParametersAsync(
            Arg.Any<GetParametersRequest>(),
            Arg.Any<CancellationToken>())
            .Returns(new GetParametersResponse
            {
                Parameters = new List<Parameter>()
            });

        var provider = new SsmJwtOptionsProvider(fallbackSecret: null, basePath: basePath, ssmClient: _ssmClient);

        // Act & Assert
        var act = async () => await provider.ObterJwtOptionsAsync();
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*jwt_secret*");
    }
}

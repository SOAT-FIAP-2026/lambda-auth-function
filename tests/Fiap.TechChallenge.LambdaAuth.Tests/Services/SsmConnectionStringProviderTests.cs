using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;
using Fiap.TechChallenge.LambdaAuth.Services;
using FluentAssertions;
using NSubstitute;

namespace Fiap.TechChallenge.LambdaAuth.Tests.Services;

public class SsmConnectionStringProviderTests
{
    private readonly IAmazonSimpleSystemsManagement _ssmClient = Substitute.For<IAmazonSimpleSystemsManagement>();

    [Fact]
    public async Task ObterConnectionStringAsync_FallbackValido_RetornaDiretamenteSemChamarSsm()
    {
        // Arrange
        const string connValida = "Host=meu-rds.aws.com;Port=5432;Database=techchallenge;Username=postgres;Password=segredo;";
        var provider = new SsmConnectionStringProvider(connValida, ssmClient: _ssmClient);

        // Act
        var resultado = await provider.ObterConnectionStringAsync();

        // Assert
        resultado.Should().Be(connValida);
        await _ssmClient.DidNotReceiveWithAnyArgs().GetParameterAsync(Arg.Any<GetParameterRequest>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Host=placeholder;Port=5432;Database=techchallenge;Username=placeholder;Password=placeholder;")]
    public async Task ObterConnectionStringAsync_FallbackNuloOuPlaceholder_ConsultaSsm(string? fallback)
    {
        // Arrange
        const string ssmConnString = "Host=real-rds.aws.com;Port=5432;Database=techchallenge;Username=admin;Password=prod_pass;";
        const string paramName = "/techchallenge/prod/db_connection_string";

        _ssmClient.GetParameterAsync(
            Arg.Is<GetParameterRequest>(r => r.Name == paramName && r.WithDecryption == true),
            Arg.Any<CancellationToken>())
            .Returns(new GetParameterResponse
            {
                Parameter = new Parameter { Name = paramName, Value = ssmConnString }
            });

        var provider = new SsmConnectionStringProvider(fallback, paramName, _ssmClient);

        // Act
        var resultado = await provider.ObterConnectionStringAsync();

        // Assert
        resultado.Should().Be(ssmConnString);
        await _ssmClient.Received(1).GetParameterAsync(
            Arg.Is<GetParameterRequest>(r => r.Name == paramName && r.WithDecryption == true),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ObterConnectionStringAsync_MultiplasChamadas_UtilizaCacheEmMemoria()
    {
        // Arrange
        const string ssmConnString = "Host=real-rds.aws.com;Port=5432;Database=techchallenge;";
        const string paramName = "/techchallenge/prod/db_connection_string";

        _ssmClient.GetParameterAsync(
            Arg.Any<GetParameterRequest>(),
            Arg.Any<CancellationToken>())
            .Returns(new GetParameterResponse
            {
                Parameter = new Parameter { Name = paramName, Value = ssmConnString }
            });

        var provider = new SsmConnectionStringProvider(null, paramName, _ssmClient);

        // Act
        var resultado1 = await provider.ObterConnectionStringAsync();
        var resultado2 = await provider.ObterConnectionStringAsync();

        // Assert
        resultado1.Should().Be(ssmConnString);
        resultado2.Should().Be(ssmConnString);
        await _ssmClient.Received(1).GetParameterAsync(Arg.Any<GetParameterRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ObterConnectionStringAsync_SsmRetornaVazio_LancaInvalidOperationException()
    {
        // Arrange
        const string paramName = "/techchallenge/prod/db_connection_string";

        _ssmClient.GetParameterAsync(
            Arg.Any<GetParameterRequest>(),
            Arg.Any<CancellationToken>())
            .Returns(new GetParameterResponse
            {
                Parameter = new Parameter { Name = paramName, Value = "" }
            });

        var provider = new SsmConnectionStringProvider(null, paramName, _ssmClient);

        // Act & Assert
        var act = async () => await provider.ObterConnectionStringAsync();
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*{paramName}*");
    }
}

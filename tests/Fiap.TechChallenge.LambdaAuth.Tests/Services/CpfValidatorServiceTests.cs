using Fiap.TechChallenge.LambdaAuth.Exceptions;
using Fiap.TechChallenge.LambdaAuth.Services;
using FluentAssertions;

namespace Fiap.TechChallenge.LambdaAuth.Tests.Services;

public class CpfValidatorServiceTests
{
    private readonly CpfValidatorService _sut = new();

    [Theory]
    [InlineData("529.982.247-25")]
    [InlineData("52998224725")]
    public void ValidarENormalizar_CpfValido_RetornaSomenteDigitos(string cpf)
    {
        var resultado = _sut.ValidarENormalizar(cpf);
        resultado.Should().Be("52998224725");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("1234567890")]
    [InlineData("123456789012")]
    [InlineData("abc.def.ghi-jk")]
    public void ValidarENormalizar_FormatoInvalido_LancaExcecao(string? cpf)
    {
        var act = () => _sut.ValidarENormalizar(cpf!);
        act.Should().Throw<CpfInvalidoException>();
    }

    [Theory]
    [InlineData("00000000000")]
    [InlineData("11111111111")]
    [InlineData("99999999999")]
    public void ValidarENormalizar_DigitosTodosIguais_LancaExcecao(string cpf)
    {
        var act = () => _sut.ValidarENormalizar(cpf);
        act.Should().Throw<CpfInvalidoException>();
    }

    [Theory]
    [InlineData("529.982.247-26")]
    [InlineData("529.982.247-35")]
    [InlineData("52998224726")]
    public void ValidarENormalizar_DigitosVerificadoresInvalidos_LancaExcecao(string cpf)
    {
        var act = () => _sut.ValidarENormalizar(cpf);
        act.Should().Throw<CpfInvalidoException>();
    }
}

using System.IdentityModel.Tokens.Jwt;
using FluentAssertions;
using Fiap.TechChallenge.LambdaAuth.Services;

namespace Fiap.TechChallenge.LambdaAuth.Tests.Services;

public class JwtServiceTests
{
    private const string Secret   = "super-secret-key-com-32-caracteres!";
    private const string Issuer   = "fiap-tech-challenge";
    private const string Audience = "fiap-api";
    private const int    Expires  = 3600;

    private readonly JwtService _sut = new(Secret, Issuer, Audience, Expires);

    [Fact]
    public void GerarToken_RetornaTokenNaoVazio()
    {
        var (token, expiresIn) = _sut.GerarToken(Guid.NewGuid(), "52998224725");
        token.Should().NotBeNullOrWhiteSpace();
        expiresIn.Should().Be(Expires);
    }

    [Fact]
    public void GerarToken_TokenContemClaimsCorretas()
    {
        var clienteId = Guid.NewGuid();
        var cpf = "52998224725";

        var (token, _) = _sut.GerarToken(clienteId, cpf);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        jwt.Issuer.Should().Be(Issuer);
        jwt.Audiences.Should().Contain(Audience);
        jwt.Subject.Should().Be(clienteId.ToString());
        jwt.Claims.Should().Contain(c => c.Type == "cpf" && c.Value == cpf);
    }

    [Fact]
    public void GerarToken_TokenExpiraNoTempoCorreto()
    {
        var (token, _) = _sut.GerarToken(Guid.NewGuid(), "52998224725");
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        var diffSegundos = (jwt.ValidTo - jwt.ValidFrom).TotalSeconds;
        diffSegundos.Should().BeApproximately(Expires, 5);
    }

    [Fact]
    public void Construtor_SecretCurto_LancaExcecao()
    {
        var act = () => new JwtService("curta", Issuer, Audience, Expires);
        act.Should().Throw<ArgumentException>().WithMessage("*32*");
    }
}

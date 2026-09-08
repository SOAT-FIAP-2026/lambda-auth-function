using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Fiap.TechChallenge.LambdaAuth.Services;

public class JwtService
{
    private readonly SigningCredentials _credentials;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly int _expiresInSeconds;

    public JwtService(string secret, string issuer, string audience, int expiresInSeconds)
    {
        if (secret.Length < 32)
            throw new ArgumentException("JWT_SECRET deve ter pelo menos 32 caracteres.", nameof(secret));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        _credentials      = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        _issuer           = issuer;
        _audience         = audience;
        _expiresInSeconds = expiresInSeconds;
    }

    public (string Token, int ExpiresIn) GerarToken(Guid clienteId, string cpf)
    {
        var agora = DateTime.UtcNow;
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, clienteId.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim("cpf", cpf),
        };

        var token = new JwtSecurityToken(
            issuer:             _issuer,
            audience:           _audience,
            claims:             claims,
            notBefore:          agora,
            expires:            agora.AddSeconds(_expiresInSeconds),
            signingCredentials: _credentials
        );

        return (new JwtSecurityTokenHandler().WriteToken(token), _expiresInSeconds);
    }
}

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Fiap.TechChallenge.LambdaAuth.Services;

public class JwtService : IJwtService
{
    private readonly IJwtOptionsProvider? _optionsProvider;
    private SigningCredentials? _credentials;
    private string? _issuer;
    private string? _audience;
    private int _expiresInSeconds;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public JwtService(string secret, string issuer, string audience, int expiresInSeconds)
    {
        ValidarEInicializar(secret, issuer, audience, expiresInSeconds);
    }

    public JwtService(IJwtOptionsProvider optionsProvider)
    {
        _optionsProvider = optionsProvider;
    }

    private void ValidarEInicializar(string secret, string issuer, string audience, int expiresInSeconds)
    {
        if (secret.Length < 32)
            throw new ArgumentException("JWT_SECRET deve ter pelo menos 32 caracteres.", nameof(secret));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        _credentials      = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        _issuer           = issuer;
        _audience         = audience;
        _expiresInSeconds = expiresInSeconds;
    }

    public async ValueTask<(string Token, int ExpiresIn)> GerarTokenAsync(
        Guid clienteId, string cpf, CancellationToken ct = default)
    {
        if (_credentials is null)
        {
            await _semaphore.WaitAsync(ct);
            try
            {
                if (_credentials is null)
                {
                    if (_optionsProvider is null)
                    {
                        throw new InvalidOperationException("Nenhum provedor de configurações JWT configurado.");
                    }

                    var options = await _optionsProvider.ObterJwtOptionsAsync(ct);
                    ValidarEInicializar(options.Secret, options.Issuer, options.Audience, options.ExpiresInSeconds);
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }

        return GerarToken(clienteId, cpf);
    }

    public (string Token, int ExpiresIn) GerarToken(Guid clienteId, string cpf)
    {
        if (_credentials is null || _issuer is null || _audience is null)
        {
            throw new InvalidOperationException("Credenciais JWT não inicializadas. Utilize GerarTokenAsync.");
        }

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

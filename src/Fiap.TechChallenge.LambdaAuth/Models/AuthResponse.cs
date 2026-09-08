using System.Text.Json.Serialization;

namespace Fiap.TechChallenge.LambdaAuth.Models;

public record AuthResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("token_type")]   string TokenType,
    [property: JsonPropertyName("expires_in")]   int ExpiresIn
);

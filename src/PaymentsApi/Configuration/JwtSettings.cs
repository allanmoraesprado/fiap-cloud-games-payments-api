namespace PaymentsApi.Configuration;

// PaymentsAPI only VALIDATES tokens (Phase 3); the values must match UsersAPI's (shared secret).
public class JwtSettings
{
    public string Issuer { get; set; } = "FiapCloudGames";
    public string Audience { get; set; } = "FiapCloudGames";
    public string SecretKey { get; set; } = string.Empty;
}

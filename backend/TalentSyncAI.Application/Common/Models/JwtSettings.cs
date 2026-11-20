namespace TalentSyncAI.Application.Common.Models;

public class JwtSettings
{
    public const string SectionName = "Jwt";

    public string SecretKey { get; set; } = string.Empty;
    public string Issuer { get; set; } = "TalentSyncAI";
    public string Audience { get; set; } = "TalentSyncAI_Client";
    public int ExpirationMinutes { get; set; } = 60;
}

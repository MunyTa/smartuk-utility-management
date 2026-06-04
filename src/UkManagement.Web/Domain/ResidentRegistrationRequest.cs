using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;

namespace UkManagement.Web.Domain;

public sealed class ResidentRegistrationRequest
{
    public int Id { get; set; }

    [MaxLength(120)]
    public required string FullName { get; set; }

    [MaxLength(24)]
    public required string ApartmentNumber { get; set; }

    [MaxLength(180)]
    public required string Email { get; set; }

    [MaxLength(32)]
    public required string Phone { get; set; }

    [MaxLength(180)]
    public required string KeycloakUsername { get; set; }

    public RegistrationRequestStatus Status { get; set; } = RegistrationRequestStatus.EmailCodeSent;

    [MaxLength(128)]
    public required string VerificationCodeHash { get; set; }

    public DateTimeOffset VerificationCodeExpiresAt { get; set; }

    public DateTimeOffset? EmailVerifiedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? ReviewedAt { get; set; }

    [MaxLength(80)]
    public string? ReviewedBy { get; set; }

    [MaxLength(500)]
    public string? ReviewComment { get; set; }

    public bool IsCodeValid(string code)
    {
        var expected = Convert.FromHexString(VerificationCodeHash);
        var actual = Convert.FromHexString(HashCode(code));
        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }

    public static string HashCode(string code)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(code.Trim()));
        return Convert.ToHexString(bytes);
    }
}

public enum RegistrationRequestStatus
{
    EmailCodeSent = 1,
    PendingApproval = 2,
    Approved = 3,
    Rejected = 4
}

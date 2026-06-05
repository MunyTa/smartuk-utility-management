using System.Security.Cryptography;
using System.Text;

namespace UkManagement.Web.Services;

public sealed class SimulatorCatalogOptions
{
    public const string DefaultHeaderName = "X-Simulator-Api-Key";

    public string? ApiKey { get; set; }
    public string HeaderName { get; set; } = DefaultHeaderName;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);

    public bool Matches(string? providedKey)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(providedKey))
        {
            return false;
        }

        var expected = Encoding.UTF8.GetBytes(ApiKey!);
        var provided = Encoding.UTF8.GetBytes(providedKey);
        return expected.Length == provided.Length
            && CryptographicOperations.FixedTimeEquals(expected, provided);
    }
}

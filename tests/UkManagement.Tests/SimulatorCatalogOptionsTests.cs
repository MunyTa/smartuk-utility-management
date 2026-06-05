using UkManagement.Web.Services;

namespace UkManagement.Tests;

public sealed class SimulatorCatalogOptionsTests
{
    [Fact]
    public void Matches_ReturnsTrueForExpectedApiKey()
    {
        var options = new SimulatorCatalogOptions
        {
            ApiKey = "secret-token"
        };

        Assert.True(options.Matches("secret-token"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("wrong-token")]
    public void Matches_ReturnsFalseForMissingOrWrongApiKey(string? providedKey)
    {
        var options = new SimulatorCatalogOptions
        {
            ApiKey = "secret-token"
        };

        Assert.False(options.Matches(providedKey));
    }
}

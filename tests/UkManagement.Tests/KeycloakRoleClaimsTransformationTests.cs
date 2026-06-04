using System.Security.Claims;
using UkManagement.Web.Services;

namespace UkManagement.Tests;

public sealed class KeycloakRoleClaimsTransformationTests
{
    [Fact]
    public async Task TransformAsync_AddsPlainRoleClaimsFromJsonArray()
    {
        var principal = CreatePrincipal(new Claim("role", "[\"Resident\"]"));
        var transformation = new KeycloakRoleClaimsTransformation();

        var transformed = await transformation.TransformAsync(principal);

        Assert.True(transformed.IsInRole("Resident"));
    }

    [Fact]
    public async Task TransformAsync_AddsPlainRoleClaimsFromMappedRoleClaim()
    {
        var principal = CreatePrincipal(new Claim(ClaimTypes.Role, "Admin"));
        var transformation = new KeycloakRoleClaimsTransformation();

        var transformed = await transformation.TransformAsync(principal);

        Assert.True(transformed.IsInRole("Admin"));
    }

    [Fact]
    public async Task TransformAsync_AddsPlainRoleClaimsFromRealmAccess()
    {
        var principal = CreatePrincipal(new Claim("realm_access", "{\"roles\":[\"Dispatcher\"]}"));
        var transformation = new KeycloakRoleClaimsTransformation();

        var transformed = await transformation.TransformAsync(principal);

        Assert.True(transformed.IsInRole("Dispatcher"));
    }

    private static ClaimsPrincipal CreatePrincipal(params Claim[] claims)
    {
        var identity = new ClaimsIdentity(claims, "test", "name", "role");
        return new ClaimsPrincipal(identity);
    }
}

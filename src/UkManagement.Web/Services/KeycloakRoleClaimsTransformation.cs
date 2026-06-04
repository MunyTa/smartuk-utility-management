using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;

namespace UkManagement.Web.Services;

public sealed class KeycloakRoleClaimsTransformation : IClaimsTransformation
{
    private static readonly string[] KnownRoleClaimTypes = ["role", "roles", ClaimTypes.Role];

    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity is not ClaimsIdentity identity || !identity.IsAuthenticated)
        {
            return Task.FromResult(principal);
        }

        var roles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var claimType in KnownRoleClaimTypes)
        {
            foreach (var claim in principal.FindAll(claimType))
            {
                AddRoleValue(claim.Value, roles);
            }
        }

        foreach (var claim in principal.FindAll("realm_access"))
        {
            AddRolesObject(claim.Value, roles);
        }

        foreach (var claim in principal.FindAll("resource_access"))
        {
            AddResourceAccessRoles(claim.Value, roles);
        }

        foreach (var role in roles)
        {
            AddClaimIfMissing(identity, identity.RoleClaimType, role);
            AddClaimIfMissing(identity, "role", role);
            AddClaimIfMissing(identity, ClaimTypes.Role, role);
        }

        return Task.FromResult(principal);
    }

    private static void AddRoleValue(string? value, ISet<string> roles)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        value = value.Trim();
        if (value.StartsWith('[') || value.StartsWith('{'))
        {
            try
            {
                using var document = JsonDocument.Parse(value);
                if (document.RootElement.ValueKind == JsonValueKind.Array)
                {
                    AddRolesArray(document.RootElement, roles);
                }
                else
                {
                    AddRolesFromObject(document.RootElement, roles);
                }

                return;
            }
            catch (JsonException)
            {
            }
        }

        foreach (var role in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            roles.Add(role);
        }
    }

    private static void AddRolesObject(string? value, ISet<string> roles)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(value);
            AddRolesFromObject(document.RootElement, roles);
        }
        catch (JsonException)
        {
        }
    }

    private static void AddResourceAccessRoles(string? value, ISet<string> roles)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(value);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            foreach (var client in document.RootElement.EnumerateObject())
            {
                AddRolesFromObject(client.Value, roles);
            }
        }
        catch (JsonException)
        {
        }
    }

    private static void AddRolesFromObject(JsonElement element, ISet<string> roles)
    {
        if (element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty("roles", out var rolesElement)
            && rolesElement.ValueKind == JsonValueKind.Array)
        {
            AddRolesArray(rolesElement, roles);
        }
    }

    private static void AddRolesArray(JsonElement element, ISet<string> roles)
    {
        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                AddRoleValue(item.GetString(), roles);
            }
        }
    }

    private static void AddClaimIfMissing(ClaimsIdentity identity, string claimType, string role)
    {
        if (!identity.HasClaim(claim => claim.Type == claimType
            && string.Equals(claim.Value, role, StringComparison.OrdinalIgnoreCase)))
        {
            identity.AddClaim(new Claim(claimType, role));
        }
    }
}

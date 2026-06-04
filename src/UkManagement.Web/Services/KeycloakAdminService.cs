using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace UkManagement.Web.Services;

public sealed class KeycloakAdminService(
    HttpClient httpClient,
    Microsoft.Extensions.Options.IOptions<KeycloakAdminOptions> options,
    ILogger<KeycloakAdminService> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly KeycloakAdminOptions _options = options.Value;

    public async Task EnsureResidentAccountAsync(
        string username,
        string email,
        string firstName,
        string lastName,
        string temporaryPassword,
        CancellationToken cancellationToken = default)
    {
        var token = await GetAccessTokenAsync(cancellationToken);
        var userId = await FindUserIdAsync(token, username, cancellationToken);
        if (userId is null)
        {
            userId = await CreateUserAsync(token, username, email, firstName, lastName, enabled: true, cancellationToken);
        }

        if (userId is null)
        {
            userId = await FindUserIdAsync(token, username, cancellationToken)
                ?? throw new InvalidOperationException("Keycloak создал пользователя, но не вернул его идентификатор.");
        }

        await SetPasswordAsync(token, userId, temporaryPassword, temporary: true, cancellationToken);
        await SetEnabledAsync(token, userId, enabled: true, cancellationToken);
        await EnsureRealmRoleAsync(token, userId, "Resident", cancellationToken);
    }

    public async Task EnsurePendingResidentAccountAsync(
        string username,
        string email,
        string firstName,
        string lastName,
        string password,
        CancellationToken cancellationToken = default)
    {
        var token = await GetAccessTokenAsync(cancellationToken);
        var userId = await FindUserIdAsync(token, username, cancellationToken);
        if (userId is null)
        {
            userId = await CreateUserAsync(token, username, email, firstName, lastName, enabled: false, cancellationToken);
        }

        if (userId is null)
        {
            userId = await FindUserIdAsync(token, username, cancellationToken)
                ?? throw new InvalidOperationException("Keycloak создал пользователя, но не вернул его идентификатор.");
        }

        await SetEnabledAsync(token, userId, enabled: false, cancellationToken);
        await SetPasswordAsync(token, userId, password, temporary: false, cancellationToken);
    }

    public async Task ApproveResidentAccountAsync(
        string username,
        CancellationToken cancellationToken = default)
    {
        var token = await GetAccessTokenAsync(cancellationToken);
        var userId = await FindUserIdAsync(token, username, cancellationToken)
            ?? throw new InvalidOperationException("Пользователь Keycloak для заявки не найден.");

        await SetEnabledAsync(token, userId, enabled: true, cancellationToken);
        await EnsureRealmRoleAsync(token, userId, "Resident", cancellationToken);
    }

    public async Task RejectResidentAccountAsync(
        string username,
        CancellationToken cancellationToken = default)
    {
        var token = await GetAccessTokenAsync(cancellationToken);
        var userId = await FindUserIdAsync(token, username, cancellationToken);
        if (userId is not null)
        {
            await SetEnabledAsync(token, userId, enabled: false, cancellationToken);
        }
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        var tokenUrl = $"{_options.BaseUrl.TrimEnd('/')}/realms/{_options.AdminRealm}/protocol/openid-connect/token";
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["client_id"] = _options.ClientId,
            ["username"] = _options.UserName,
            ["password"] = _options.Password
        });

        using var response = await httpClient.PostAsync(tokenUrl, content, cancellationToken);
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Keycloak admin token request failed: {Status} {Body}", response.StatusCode, responseText);
            throw new InvalidOperationException("Не удалось получить административный токен Keycloak.");
        }

        using var json = JsonDocument.Parse(responseText);
        return json.RootElement.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("Keycloak не вернул access_token.");
    }

    private async Task<string?> FindUserIdAsync(
        string token,
        string username,
        CancellationToken cancellationToken)
    {
        var url = $"{AdminBaseUrl}/users?username={Uri.EscapeDataString(username)}&exact=true";
        using var request = CreateAuthorizedRequest(HttpMethod.Get, url, token);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Keycloak user search failed: {Status} {Body}", response.StatusCode, responseText);
            throw new InvalidOperationException("Не удалось проверить пользователя в Keycloak.");
        }

        var userId = TryFindUserId(responseText, username);
        if (userId is not null)
        {
            return userId;
        }

        var searchUrl = $"{AdminBaseUrl}/users?search={Uri.EscapeDataString(username)}";
        using var searchRequest = CreateAuthorizedRequest(HttpMethod.Get, searchUrl, token);
        using var searchResponse = await httpClient.SendAsync(searchRequest, cancellationToken);
        var searchText = await searchResponse.Content.ReadAsStringAsync(cancellationToken);
        if (!searchResponse.IsSuccessStatusCode)
        {
            logger.LogWarning("Keycloak user fallback search failed: {Status} {Body}", searchResponse.StatusCode, searchText);
            throw new InvalidOperationException("Не удалось проверить пользователя в Keycloak.");
        }

        return TryFindUserId(searchText, username);
    }

    private async Task<string?> CreateUserAsync(
        string token,
        string username,
        string email,
        string firstName,
        string lastName,
        bool enabled,
        CancellationToken cancellationToken)
    {
        using var request = CreateAuthorizedRequest(HttpMethod.Post, $"{AdminBaseUrl}/users", token);
        request.Content = JsonContent.Create(new
        {
            username,
            enabled,
            email,
            firstName,
            lastName
        }, options: JsonOptions);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return TryGetUserIdFromLocation(response.Headers.Location);
        }

        if (response.StatusCode != System.Net.HttpStatusCode.Conflict)
        {
            logger.LogWarning("Keycloak user create failed: {Status} {Body}", response.StatusCode, responseText);
            throw new InvalidOperationException("Не удалось создать пользователя в Keycloak.");
        }

        return null;
    }

    private async Task SetPasswordAsync(
        string token,
        string userId,
        string password,
        bool temporary,
        CancellationToken cancellationToken)
    {
        using var request = CreateAuthorizedRequest(
            HttpMethod.Put,
            $"{AdminBaseUrl}/users/{Uri.EscapeDataString(userId)}/reset-password",
            token);
        request.Content = JsonContent.Create(new
        {
            type = "password",
            value = password,
            temporary
        }, options: JsonOptions);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Keycloak password reset failed: {Status} {Body}", response.StatusCode, responseText);
            throw new InvalidOperationException("Не удалось задать временный пароль в Keycloak.");
        }
    }

    private async Task SetEnabledAsync(
        string token,
        string userId,
        bool enabled,
        CancellationToken cancellationToken)
    {
        using var request = CreateAuthorizedRequest(
            HttpMethod.Put,
            $"{AdminBaseUrl}/users/{Uri.EscapeDataString(userId)}",
            token);
        request.Content = JsonContent.Create(new { enabled }, options: JsonOptions);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Keycloak user enabled update failed: {Status} {Body}", response.StatusCode, responseText);
            throw new InvalidOperationException("Не удалось изменить состояние пользователя Keycloak.");
        }
    }

    private async Task EnsureRealmRoleAsync(
        string token,
        string userId,
        string roleName,
        CancellationToken cancellationToken)
    {
        using var roleRequest = CreateAuthorizedRequest(
            HttpMethod.Get,
            $"{AdminBaseUrl}/roles/{Uri.EscapeDataString(roleName)}",
            token);
        using var roleResponse = await httpClient.SendAsync(roleRequest, cancellationToken);
        var roleText = await roleResponse.Content.ReadAsStringAsync(cancellationToken);
        if (!roleResponse.IsSuccessStatusCode)
        {
            logger.LogWarning("Keycloak role lookup failed: {Status} {Body}", roleResponse.StatusCode, roleText);
            throw new InvalidOperationException($"В Keycloak не найдена роль {roleName}.");
        }

        using var roleJson = JsonDocument.Parse(roleText);
        var role = roleJson.RootElement;
        var rolePayload = new[]
        {
            new
            {
                id = role.GetProperty("id").GetString(),
                name = role.GetProperty("name").GetString()
            }
        };

        using var assignRequest = CreateAuthorizedRequest(
            HttpMethod.Post,
            $"{AdminBaseUrl}/users/{Uri.EscapeDataString(userId)}/role-mappings/realm",
            token);
        assignRequest.Content = JsonContent.Create(rolePayload, options: JsonOptions);

        using var assignResponse = await httpClient.SendAsync(assignRequest, cancellationToken);
        var assignText = await assignResponse.Content.ReadAsStringAsync(cancellationToken);
        if (!assignResponse.IsSuccessStatusCode && assignResponse.StatusCode != System.Net.HttpStatusCode.Conflict)
        {
            logger.LogWarning("Keycloak role assign failed: {Status} {Body}", assignResponse.StatusCode, assignText);
            throw new InvalidOperationException($"Не удалось назначить роль {roleName} пользователю Keycloak.");
        }
    }

    private HttpRequestMessage CreateAuthorizedRequest(HttpMethod method, string url, string token)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private static string? TryFindUserId(string responseText, string username)
    {
        using var json = JsonDocument.Parse(responseText);
        if (json.RootElement.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        JsonElement? firstUser = null;
        foreach (var user in json.RootElement.EnumerateArray())
        {
            firstUser ??= user;
            if (user.TryGetProperty("username", out var usernameProperty)
                && string.Equals(usernameProperty.GetString(), username, StringComparison.OrdinalIgnoreCase)
                && user.TryGetProperty("id", out var idProperty))
            {
                return idProperty.GetString();
            }
        }

        if (firstUser is { } fallback
            && fallback.TryGetProperty("id", out var fallbackId))
        {
            return fallbackId.GetString();
        }

        return null;
    }

    private static string? TryGetUserIdFromLocation(Uri? location)
    {
        if (location is null)
        {
            return null;
        }

        var path = location.IsAbsoluteUri ? location.AbsolutePath : location.OriginalString;
        var lastSlash = path.LastIndexOf('/');
        return lastSlash < 0 || lastSlash == path.Length - 1
            ? null
            : Uri.UnescapeDataString(path[(lastSlash + 1)..]);
    }

    private string AdminBaseUrl => $"{_options.BaseUrl.TrimEnd('/')}/admin/realms/{_options.Realm}";
}

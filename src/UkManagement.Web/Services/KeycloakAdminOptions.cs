namespace UkManagement.Web.Services;

public sealed class KeycloakAdminOptions
{
    public string BaseUrl { get; set; } = "http://keycloak:8080";
    public string Realm { get; set; } = "uk-management";
    public string AdminRealm { get; set; } = "master";
    public string ClientId { get; set; } = "admin-cli";
    public string UserName { get; set; } = "admin";
    public string Password { get; set; } = "admin";
}

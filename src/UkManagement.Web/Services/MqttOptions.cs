namespace UkManagement.Web.Services;

public sealed class MqttOptions
{
    public bool Enabled { get; set; } = true;
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 1883;
    public string Topic { get; set; } = "uk/meters/readings";
    public string ClientId { get; set; } = "uk-management-web";
}

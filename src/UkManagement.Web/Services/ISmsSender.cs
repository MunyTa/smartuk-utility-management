namespace UkManagement.Web.Services;

public interface ISmsSender
{
    Task<string> SendAsync(string phone, string message, CancellationToken cancellationToken = default);
}

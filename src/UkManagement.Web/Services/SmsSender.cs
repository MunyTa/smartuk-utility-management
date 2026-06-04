using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace UkManagement.Web.Services;

public sealed class SmsSender(HttpClient httpClient, IOptions<SmsOptions> options) : ISmsSender
{
    private readonly SmsOptions _options = options.Value;

    public async Task<string> SendAsync(
        string phone,
        string message,
        CancellationToken cancellationToken = default)
    {
        if (!_options.IsConfigured)
        {
            throw new InvalidOperationException("Не настроены логин и API-ключ SMS Aero.");
        }

        var normalizedPhone = NormalizeRussianPhone(phone);
        var query = new Dictionary<string, string?>
        {
            ["number"] = normalizedPhone,
            ["text"] = message,
            ["sign"] = string.IsNullOrWhiteSpace(_options.Sender) ? "SMSAero" : _options.Sender
        };

        if (_options.TestMode)
        {
            query["test"] = "1";
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            QueryHelpers.AddQueryString("https://gate.smsaero.ru/v2/sms/send", query));

        var authBytes = Encoding.ASCII.GetBytes($"{_options.Login}:{_options.ApiKey}");
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(authBytes));

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(responseText);
        var root = json.RootElement;

        if (root.TryGetProperty("success", out var successElement) && !successElement.GetBoolean())
        {
            var messageText = root.TryGetProperty("message", out var messageElement)
                ? messageElement.GetString()
                : "SMS Aero отклонил запрос.";
            throw new InvalidOperationException(messageText);
        }

        string? smsId = null;
        string? status = null;
        string? extendedStatus = null;
        if (root.TryGetProperty("data", out var dataElement)
            && dataElement.ValueKind == JsonValueKind.Object
            && dataElement.TryGetProperty("id", out var idElement))
        {
            smsId = idElement.ToString();
        }

        if (root.TryGetProperty("data", out dataElement)
            && dataElement.ValueKind == JsonValueKind.Object)
        {
            if (dataElement.TryGetProperty("status", out var statusElement))
            {
                status = statusElement.ToString();
            }

            if (dataElement.TryGetProperty("extendStatus", out var extendedStatusElement))
            {
                extendedStatus = extendedStatusElement.GetString();
            }
        }

        var statusText = string.IsNullOrWhiteSpace(extendedStatus)
            ? status
            : $"{extendedStatus} ({status})";

        return string.IsNullOrWhiteSpace(smsId)
            ? $"Принято SMS Aero на {normalizedPhone}, статус {statusText}"
            : $"Принято SMS Aero на {normalizedPhone}, id {smsId}, статус {statusText}";
    }

    private static string NormalizeRussianPhone(string phone)
    {
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.Length == 11 && digits.StartsWith('8'))
        {
            digits = $"7{digits[1..]}";
        }

        if (digits.Length != 11 || !digits.StartsWith('7'))
        {
            throw new InvalidOperationException("SMS принимается на российский номер в формате 79XXXXXXXXX.");
        }

        return digits;
    }
}

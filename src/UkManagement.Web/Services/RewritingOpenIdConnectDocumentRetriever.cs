using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Protocols;

namespace UkManagement.Web.Services;

public sealed class RewritingOpenIdConnectDocumentRetriever(
    string publicAuthority,
    string internalAuthority) : IDocumentRetriever
{
    public async Task<string> GetDocumentAsync(string address, CancellationToken cancel)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        var document = await client.GetStringAsync(address, cancel);

        return address.Contains(".well-known/openid-configuration", StringComparison.OrdinalIgnoreCase)
            ? RewriteMetadata(document)
            : document;
    }

    private string RewriteMetadata(string document)
    {
        using var json = JsonDocument.Parse(document);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            WriteElement(writer, json.RootElement, propertyName: null);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private void WriteElement(Utf8JsonWriter writer, JsonElement element, string? propertyName)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject())
                {
                    writer.WritePropertyName(property.Name);
                    WriteElement(writer, property.Value, property.Name);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    WriteElement(writer, item, propertyName: null);
                }
                writer.WriteEndArray();
                break;
            case JsonValueKind.String:
                var value = element.GetString();
                if (propertyName != "issuer"
                    && value is not null
                    && value.StartsWith(publicAuthority, StringComparison.OrdinalIgnoreCase))
                {
                    value = internalAuthority.TrimEnd('/') + value[publicAuthority.TrimEnd('/').Length..];
                }

                writer.WriteStringValue(value);
                break;
            case JsonValueKind.Number:
                writer.WriteRawValue(element.GetRawText());
                break;
            case JsonValueKind.True:
            case JsonValueKind.False:
                writer.WriteBooleanValue(element.GetBoolean());
                break;
            case JsonValueKind.Null:
                writer.WriteNullValue();
                break;
            default:
                writer.WriteRawValue(element.GetRawText());
                break;
        }
    }
}

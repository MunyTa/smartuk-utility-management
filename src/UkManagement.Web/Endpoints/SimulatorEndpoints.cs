using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using UkManagement.Web.Data;
using UkManagement.Web.Services;

namespace UkManagement.Web.Endpoints;

public static class SimulatorEndpoints
{
    public static void MapSimulatorEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/simulator/meters", async (
            HttpRequest request,
            AppDbContext db,
            IOptions<SimulatorCatalogOptions> options,
            CancellationToken cancellationToken) =>
        {
            var catalogOptions = options.Value;
            if (!catalogOptions.IsConfigured)
            {
                return Results.Problem(
                    "API-ключ каталога симулятора не настроен.",
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            var providedKey = request.Headers[catalogOptions.HeaderName].FirstOrDefault();
            if (!catalogOptions.Matches(providedKey))
            {
                return Results.Unauthorized();
            }

            var meters = await db.Meters
                .Include(x => x.Apartment)
                .OrderBy(x => x.Apartment.Number)
                .ThenBy(x => x.Type)
                .Select(x => new
                {
                    deviceId = x.ExternalDeviceId,
                    unit = x.Unit,
                    meterType = x.Type.ToString(),
                    serialNumber = x.SerialNumber,
                    apartmentNumber = x.Apartment.Number,
                    floor = x.Apartment.Floor,
                    lastValue = x.LastValue
                })
                .ToListAsync(cancellationToken);

            return Results.Ok(meters);
        });
    }
}

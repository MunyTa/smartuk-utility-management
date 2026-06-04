using Microsoft.EntityFrameworkCore;
using UkManagement.Web.Data;

namespace UkManagement.Web.Endpoints;

public static class SimulatorEndpoints
{
    public static void MapSimulatorEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/simulator/meters", async (
            AppDbContext db,
            CancellationToken cancellationToken) =>
        {
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
        }).AllowAnonymous();
    }
}

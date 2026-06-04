using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using UkManagement.Web.Data;
using UkManagement.Web.Domain;

namespace UkManagement.Web.Pages.Admin.Apartments;

public sealed class IndexModel(AppDbContext db) : PageModel
{
    public IReadOnlyList<ApartmentRow> Apartments { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var apartments = await db.Apartments
            .Include(x => x.Building)
            .Include(x => x.Residents)
            .Include(x => x.Meters)
            .OrderBy(x => x.Number)
            .ToListAsync(cancellationToken);

        var apartmentIds = apartments.Select(x => x.Id).ToArray();
        var activeRequests = await db.ServiceRequests
            .Include(x => x.Resident)
            .Where(x => apartmentIds.Contains(x.Resident.ApartmentId)
                && x.Status != ServiceRequestStatus.Completed
                && x.Status != ServiceRequestStatus.Cancelled)
            .GroupBy(x => x.Resident.ApartmentId)
            .Select(x => new { ApartmentId = x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.ApartmentId, x => x.Count, cancellationToken);

        var meterIds = apartments.SelectMany(x => x.Meters).Select(x => x.Id).ToArray();
        var latestReadings = (await db.MeterReadings
            .Include(x => x.Meter)
            .Where(x => meterIds.Contains(x.MeterId))
            .OrderByDescending(x => x.ReceivedAt)
            .ToListAsync(cancellationToken))
            .GroupBy(x => x.Meter.ApartmentId)
            .ToDictionary(x => x.Key, x => x.First());

        Apartments = apartments
            .Select(apartment =>
            {
                activeRequests.TryGetValue(apartment.Id, out var requestCount);
                latestReadings.TryGetValue(apartment.Id, out var latestReading);
                return new ApartmentRow(apartment, requestCount, latestReading);
            })
            .ToList();
    }

    public sealed record ApartmentRow(
        Apartment Apartment,
        int ActiveRequestCount,
        MeterReading? LatestReading);
}

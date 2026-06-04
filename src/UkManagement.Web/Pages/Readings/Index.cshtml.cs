using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using UkManagement.Web.Data;
using UkManagement.Web.Domain;

namespace UkManagement.Web.Pages.Readings;

public sealed class IndexModel(AppDbContext db) : PageModel
{
    public IReadOnlyList<MeterReading> Readings { get; private set; } = [];
    public IReadOnlyList<SelectListItem> ApartmentOptions { get; private set; } = [];
    public IReadOnlyList<SelectListItem> FloorOptions { get; private set; } = [];
    public IReadOnlyList<SelectListItem> MeterTypeOptions { get; private set; } = [];
    public IReadOnlyList<SelectListItem> SortOptions { get; private set; } =
    [
        new("Сначала новые", "received_desc"),
        new("Сначала старые", "received_asc"),
        new("По квартире", "apartment"),
        new("По этажу", "floor"),
        new("По типу прибора", "meter_type")
    ];

    public int RetentionDays { get; private set; }

    [BindProperty(SupportsGet = true)]
    public string? ApartmentNumber { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? Floor { get; set; }

    [BindProperty(SupportsGet = true)]
    public MeterType? MeterType { get; set; }

    [BindProperty(SupportsGet = true)]
    public string Sort { get; set; } = "received_desc";

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        RetentionDays = await db.SystemSettings
            .Where(x => x.Id == SystemSettings.SingletonId)
            .Select(x => x.MeterReadingRetentionDays)
            .FirstOrDefaultAsync(cancellationToken);
        if (RetentionDays == 0)
        {
            RetentionDays = 1;
        }

        await LoadFilterOptionsAsync(cancellationToken);

        var query = db.MeterReadings
            .Include(x => x.Meter)
            .ThenInclude(x => x.Apartment)
            .ThenInclude(x => x.Building)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(ApartmentNumber))
        {
            query = query.Where(x => x.Meter.Apartment.Number == ApartmentNumber);
        }

        if (Floor.HasValue)
        {
            query = query.Where(x => x.Meter.Apartment.Floor == Floor.Value);
        }

        if (MeterType.HasValue)
        {
            query = query.Where(x => x.Meter.Type == MeterType.Value);
        }

        query = Sort switch
        {
            "received_asc" => query
                .OrderBy(x => x.ReceivedAt),
            "apartment" => query
                .OrderBy(x => x.Meter.Apartment.Number)
                .ThenByDescending(x => x.ReceivedAt),
            "floor" => query
                .OrderBy(x => x.Meter.Apartment.Floor)
                .ThenBy(x => x.Meter.Apartment.Number)
                .ThenByDescending(x => x.ReceivedAt),
            "meter_type" => query
                .OrderBy(x => x.Meter.Type)
                .ThenBy(x => x.Meter.Apartment.Number)
                .ThenByDescending(x => x.ReceivedAt),
            _ => query
                .OrderByDescending(x => x.ReceivedAt)
        };

        Readings = await query
            .Take(100)
            .ToListAsync(cancellationToken);
    }

    private async Task LoadFilterOptionsAsync(CancellationToken cancellationToken)
    {
        ApartmentOptions = await db.Apartments
            .OrderBy(x => x.Number)
            .Select(x => new SelectListItem($"кв. {x.Number}", x.Number))
            .ToListAsync(cancellationToken);

        FloorOptions = await db.Apartments
            .Select(x => x.Floor)
            .Distinct()
            .OrderBy(x => x)
            .Select(x => new SelectListItem($"{x} этаж", x.ToString()))
            .ToListAsync(cancellationToken);

        var meterTypes = await db.Meters
            .Select(x => x.Type)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);
        MeterTypeOptions = meterTypes
            .Select(x => new SelectListItem(x.ToDisplayName(), x.ToString()))
            .ToList();
    }
}

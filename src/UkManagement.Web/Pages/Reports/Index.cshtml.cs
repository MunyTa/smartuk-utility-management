using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using UkManagement.Web.Data;
using UkManagement.Web.Domain;
using UkManagement.Web.Services.Reports;

namespace UkManagement.Web.Pages.Reports;

public sealed class IndexModel(AppDbContext db) : PageModel
{
    private const decimal LowBatteryVoltage = 2.8m;

    public IReadOnlyList<SelectListItem> ApartmentOptions { get; private set; } = [];
    public IReadOnlyList<SelectListItem> MeterTypeOptions { get; private set; } = [];
    public IReadOnlyList<SelectListItem> ReportTypeOptions { get; private set; } = [];
    public IReadOnlyList<ConsumptionReportRow> ConsumptionRows { get; private set; } = [];
    public IReadOnlyList<ConsumptionSummaryRow> ConsumptionSummary { get; private set; } = [];
    public IReadOnlyList<CriticalReportRow> CriticalRows { get; private set; } = [];

    public int ApartmentCount { get; private set; }
    public int MeterCount { get; private set; }
    public int ReadingsCount { get; private set; }
    public int ProblemRowsCount { get; private set; }
    public int MetersWithoutDataCount { get; private set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? From { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? To { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? ApartmentId { get; set; }

    [BindProperty(SupportsGet = true)]
    public MeterType? MeterType { get; set; }

    [BindProperty(SupportsGet = true)]
    public UtilityReportType ReportType { get; set; } = UtilityReportType.Consumption;

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        EnsurePeriod();
        await LoadOptionsAsync(cancellationToken);
        await LoadReportAsync(cancellationToken);
    }

    public async Task<IActionResult> OnGetWordAsync(CancellationToken cancellationToken)
    {
        EnsurePeriod();
        await LoadOptionsAsync(cancellationToken);
        await LoadReportAsync(cancellationToken);

        var content = WordReportBuilder.Build(
            ReportType,
            From!.Value,
            To!.Value,
            SelectedApartmentLabel(),
            SelectedMeterTypeLabel(),
            ConsumptionRows,
            ConsumptionSummary,
            CriticalRows);

        var reportKind = ReportType == UtilityReportType.Critical ? "critical" : "consumption";
        var fileName = $"smartuk-{reportKind}-{From:yyyyMMdd}-{To:yyyyMMdd}.docx";
        return File(
            content,
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            fileName);
    }

    private async Task LoadOptionsAsync(CancellationToken cancellationToken)
    {
        var apartments = await db.Apartments
            .OrderBy(x => x.Number)
            .Select(x => new { x.Id, x.Number })
            .ToListAsync(cancellationToken);

        ApartmentOptions = [new SelectListItem("Все квартиры", string.Empty),
            .. apartments.Select(x => new SelectListItem($"Квартира {x.Number}", x.Id.ToString()))];

        MeterTypeOptions = [new SelectListItem("Все приборы", string.Empty),
            .. Enum.GetValues<MeterType>()
                .Select(x => new SelectListItem(x.ToDisplayName(), x.ToString()))];

        ReportTypeOptions =
        [
            new SelectListItem("Потребление ресурсов", UtilityReportType.Consumption.ToString()),
            new SelectListItem("Проблемные показания", UtilityReportType.Critical.ToString())
        ];
    }

    private async Task LoadReportAsync(CancellationToken cancellationToken)
    {
        var fromOffset = DisplayTime.StartOfDayUtc(From!.Value);
        var toOffset = DisplayTime.StartOfDayUtc(To!.Value.AddDays(1));

        var metersQuery = db.Meters
            .Include(x => x.Apartment)
            .ThenInclude(x => x.Building)
            .Include(x => x.Apartment)
            .ThenInclude(x => x.Residents)
            .AsQueryable();

        if (ApartmentId is not null)
        {
            metersQuery = metersQuery.Where(x => x.ApartmentId == ApartmentId.Value);
        }

        if (MeterType is not null)
        {
            metersQuery = metersQuery.Where(x => x.Type == MeterType.Value);
        }

        var meters = await metersQuery
            .OrderBy(x => x.Apartment.Number)
            .ThenBy(x => x.Type)
            .ThenBy(x => x.SerialNumber)
            .ToListAsync(cancellationToken);

        var meterIds = meters.Select(x => x.Id).ToArray();
        List<MeterReading> readings = meterIds.Length == 0
            ? []
            : await db.MeterReadings
                .Where(x => meterIds.Contains(x.MeterId)
                            && x.MeasuredAt >= fromOffset
                            && x.MeasuredAt < toOffset)
                .OrderBy(x => x.MeasuredAt)
                .ToListAsync(cancellationToken);

        ApartmentCount = meters.Select(x => x.ApartmentId).Distinct().Count();
        MeterCount = meters.Count;
        ReadingsCount = readings.Count;

        var readingsByMeter = readings
            .GroupBy(x => x.MeterId)
            .ToDictionary(x => x.Key, x => x.OrderBy(r => r.MeasuredAt).ToList());

        var consumptionRows = new List<ConsumptionReportRow>();
        var criticalRows = new List<CriticalReportRow>();

        foreach (var meter in meters)
        {
            readingsByMeter.TryGetValue(meter.Id, out var periodReadings);
            periodReadings ??= [];

            var firstReading = periodReadings.FirstOrDefault();
            var lastReading = periodReadings.LastOrDefault();
            var consumption = firstReading is null || lastReading is null || lastReading.Value < firstReading.Value
                ? (decimal?)null
                : lastReading.Value - firstReading.Value;
            var problemReadingsCount = periodReadings.Count(IsProblemReading);

            consumptionRows.Add(new ConsumptionReportRow(
                meter.Apartment.Number,
                meter.Apartment.Building.Address,
                JoinOrDash(meter.Apartment.Residents.Select(x => x.FullName)),
                JoinOrDash(meter.Apartment.Residents.Select(x => x.Email)),
                JoinOrDash(meter.Apartment.Residents.Select(x => x.Phone)),
                meter.Type.ToDisplayName(),
                meter.SerialNumber,
                meter.ExternalDeviceId,
                meter.Unit,
                meter.Status,
                firstReading?.Value,
                firstReading?.MeasuredAt,
                lastReading?.Value,
                lastReading?.MeasuredAt,
                consumption,
                periodReadings.Count,
                problemReadingsCount,
                BuildQualitySummary(periodReadings, consumption)));

            AddCriticalRows(criticalRows, meter, periodReadings);
        }

        ConsumptionRows = consumptionRows;
        ConsumptionSummary = consumptionRows
            .Where(x => x.Consumption is not null)
            .GroupBy(x => new { x.MeterType, x.Unit })
            .Select(x => new ConsumptionSummaryRow(
                x.Key.MeterType,
                x.Key.Unit,
                x.Sum(r => r.Consumption ?? 0),
                x.Count(),
                x.Sum(r => r.ReadingsCount)))
            .OrderBy(x => x.MeterType)
            .ToList();

        CriticalRows = criticalRows
            .OrderBy(x => x.ApartmentNumber)
            .ThenBy(x => x.MeterType)
            .ThenBy(x => x.MeasuredAt)
            .ToList();
        ProblemRowsCount = CriticalRows.Count;
        MetersWithoutDataCount = consumptionRows.Count(x => x.ReadingsCount == 0);
    }

    private static void AddCriticalRows(
        List<CriticalReportRow> rows,
        Meter meter,
        IReadOnlyList<MeterReading> readings)
    {
        var residents = JoinOrDash(meter.Apartment.Residents.Select(x => x.FullName));
        var emails = JoinOrDash(meter.Apartment.Residents.Select(x => x.Email));
        var meterType = meter.Type.ToDisplayName();

        if (readings.Count == 0)
        {
            rows.Add(new CriticalReportRow(
                meter.Apartment.Number,
                residents,
                emails,
                meterType,
                meter.SerialNumber,
                "Нет данных за период",
                meter.LastValue?.ToString("0.###"),
                meter.Unit,
                meter.LastReadingAt,
                "За выбранный период показания от прибора не поступали."));
        }

        if (meter.Status != MeterStatus.Online)
        {
            rows.Add(new CriticalReportRow(
                meter.Apartment.Number,
                residents,
                emails,
                meterType,
                meter.SerialNumber,
                $"Статус прибора: {meter.Status.ToDisplayName()}",
                meter.LastValue?.ToString("0.###"),
                meter.Unit,
                meter.LastReadingAt,
                "Прибор требует проверки состояния или связи."));
        }

        foreach (var reading in readings.Where(IsProblemReading))
        {
            var problem = reading.Quality != ReadingQuality.Normal
                ? $"Качество показания: {reading.Quality.ToDisplayName()}"
                : "Низкий заряд батареи";

            rows.Add(new CriticalReportRow(
                meter.Apartment.Number,
                residents,
                emails,
                meterType,
                meter.SerialNumber,
                problem,
                reading.Value.ToString("0.###"),
                meter.Unit,
                reading.MeasuredAt,
                BuildReadingDetails(reading)));
        }
    }

    private static string BuildQualitySummary(
        IReadOnlyList<MeterReading> readings,
        decimal? consumption)
    {
        if (readings.Count == 0)
        {
            return "Нет данных за период";
        }

        var parts = new List<string>();
        var anomalies = readings.Count(x => x.Quality == ReadingQuality.Anomaly);
        var invalid = readings.Count(x => x.Quality == ReadingQuality.Invalid);
        var lowBattery = readings.Count(x => x.BatteryVoltage is <= LowBatteryVoltage);

        if (anomalies > 0)
        {
            parts.Add($"аномалий: {anomalies}");
        }

        if (invalid > 0)
        {
            parts.Add($"ошибок: {invalid}");
        }

        if (lowBattery > 0)
        {
            parts.Add($"низкий заряд: {lowBattery}");
        }

        if (consumption is null)
        {
            parts.Add("расход не рассчитан");
        }

        return parts.Count == 0 ? "Норма" : string.Join(", ", parts);
    }

    private static bool IsProblemReading(MeterReading reading)
    {
        return reading.Quality != ReadingQuality.Normal
               || reading.BatteryVoltage is <= LowBatteryVoltage;
    }

    private static string BuildReadingDetails(MeterReading reading)
    {
        var details = new List<string>();
        if (reading.SignalRssi is not null)
        {
            details.Add($"RSSI: {reading.SignalRssi}");
        }

        if (reading.BatteryVoltage is not null)
        {
            details.Add($"Батарея: {reading.BatteryVoltage:0.##} В");
        }

        return details.Count == 0 ? "Дополнительные параметры не переданы." : string.Join("; ", details);
    }

    private void EnsurePeriod()
    {
        var today = DisplayTime.Today;
        From ??= new DateTime(today.Year, today.Month, 1);
        To ??= today;

        if (From > To)
        {
            (From, To) = (To, From);
        }
    }

    private string SelectedApartmentLabel()
    {
        return ApartmentId is null
            ? "Все квартиры"
            : ApartmentOptions.FirstOrDefault(x => x.Value == ApartmentId.Value.ToString())?.Text ?? "Выбранная квартира";
    }

    private string SelectedMeterTypeLabel()
    {
        return MeterType is null ? "Все приборы" : MeterType.Value.ToDisplayName();
    }

    private static string JoinOrDash(IEnumerable<string> values)
    {
        var result = string.Join(", ", values.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct());
        return string.IsNullOrWhiteSpace(result) ? "-" : result;
    }
}

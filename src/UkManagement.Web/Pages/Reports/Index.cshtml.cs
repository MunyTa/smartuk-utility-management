using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using UkManagement.Web.Data;
using UkManagement.Web.Domain;

namespace UkManagement.Web.Pages.Reports;

public sealed class IndexModel(AppDbContext db) : PageModel
{
    public int ReadingsCount { get; private set; }
    public int AnomalyReadingsCount { get; private set; }
    public int RequestsCount { get; private set; }
    public int CompletedRequestsCount { get; private set; }
    public int SentNotificationsCount { get; private set; }
    public IReadOnlyList<ReadingTypeRow> ReadingsByType { get; private set; } = [];
    public IReadOnlyList<RequestStatusRow> RequestsByStatus { get; private set; } = [];

    [BindProperty(SupportsGet = true)]
    public DateTime? From { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? To { get; set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        EnsurePeriod();
        await LoadReportAsync(cancellationToken);
    }

    public async Task<IActionResult> OnGetExportAsync(CancellationToken cancellationToken)
    {
        EnsurePeriod();
        await LoadReportAsync(cancellationToken);

        var csv = new StringBuilder();
        csv.Append('\uFEFF');
        csv.AppendLine("Раздел;Показатель;Значение");
        csv.AppendLine($"Сводка;Показаний;{ReadingsCount}");
        csv.AppendLine($"Сводка;Аномалий;{AnomalyReadingsCount}");
        csv.AppendLine($"Сводка;Заявок;{RequestsCount}");
        csv.AppendLine($"Сводка;Выполненных заявок;{CompletedRequestsCount}");
        csv.AppendLine($"Сводка;Отправленных уведомлений;{SentNotificationsCount}");

        foreach (var row in ReadingsByType)
        {
            csv.AppendLine($"Показания по типам;{Escape(row.MeterType)};{row.Count}");
        }

        foreach (var row in RequestsByStatus)
        {
            csv.AppendLine($"Заявки по статусам;{Escape(row.Status)};{row.Count}");
        }

        var fileName = $"smartuk-report-{From:yyyyMMdd}-{To:yyyyMMdd}.csv";
        return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv; charset=utf-8", fileName);
    }

    private async Task LoadReportAsync(CancellationToken cancellationToken)
    {
        var fromOffset = DisplayTime.StartOfDayUtc(From!.Value);
        var toOffset = DisplayTime.StartOfDayUtc(To!.Value.AddDays(1));

        ReadingsCount = await db.MeterReadings.CountAsync(
            x => x.ReceivedAt >= fromOffset && x.ReceivedAt < toOffset,
            cancellationToken);
        AnomalyReadingsCount = await db.MeterReadings.CountAsync(
            x => x.ReceivedAt >= fromOffset
                 && x.ReceivedAt < toOffset
                 && x.Quality != ReadingQuality.Normal,
            cancellationToken);
        RequestsCount = await db.ServiceRequests.CountAsync(
            x => x.CreatedAt >= fromOffset && x.CreatedAt < toOffset,
            cancellationToken);
        CompletedRequestsCount = await db.ServiceRequests.CountAsync(
            x => x.ClosedAt >= fromOffset
                 && x.ClosedAt < toOffset
                 && x.Status == ServiceRequestStatus.Completed,
            cancellationToken);
        SentNotificationsCount = await db.Notifications.CountAsync(
            x => x.SentAt >= fromOffset
                 && x.SentAt < toOffset
                 && x.Status == NotificationStatus.Sent,
            cancellationToken);

        var readingsByType = await db.MeterReadings
            .Include(x => x.Meter)
            .Where(x => x.ReceivedAt >= fromOffset && x.ReceivedAt < toOffset)
            .GroupBy(x => x.Meter.Type)
            .Select(x => new { MeterType = x.Key, Count = x.Count() })
            .OrderBy(x => x.MeterType)
            .ToListAsync(cancellationToken);
        ReadingsByType = readingsByType
            .Select(x => new ReadingTypeRow(x.MeterType.ToDisplayName(), x.Count))
            .ToList();

        var requestsByStatus = await db.ServiceRequests
            .Where(x => x.CreatedAt >= fromOffset && x.CreatedAt < toOffset)
            .GroupBy(x => x.Status)
            .Select(x => new { Status = x.Key, Count = x.Count() })
            .OrderBy(x => x.Status)
            .ToListAsync(cancellationToken);
        RequestsByStatus = requestsByStatus
            .Select(x => new RequestStatusRow(x.Status.ToDisplayName(), x.Count))
            .ToList();
    }

    private void EnsurePeriod()
    {
        From ??= DisplayTime.Today;
        To ??= DisplayTime.Today;
    }

    private static string Escape(string value)
    {
        return value.Contains(';') || value.Contains('"')
            ? $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\""
            : value;
    }

    public sealed record ReadingTypeRow(string MeterType, int Count);

    public sealed record RequestStatusRow(string Status, int Count);
}

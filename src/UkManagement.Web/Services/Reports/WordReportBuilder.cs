using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using UkManagement.Web.Domain;

namespace UkManagement.Web.Services.Reports;

public static class WordReportBuilder
{
    private static readonly CultureInfo RuCulture = CultureInfo.GetCultureInfo("ru-RU");

    public static byte[] Build(
        UtilityReportType reportType,
        DateTime from,
        DateTime to,
        string apartmentFilter,
        string meterTypeFilter,
        IReadOnlyList<ConsumptionReportRow> consumptionRows,
        IReadOnlyList<ConsumptionSummaryRow> summaryRows,
        IReadOnlyList<CriticalReportRow> criticalRows)
    {
        using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(
                   stream,
                   WordprocessingDocumentType.Document))
        {
            var mainPart = document.AddMainDocumentPart();
            mainPart.Document = new Document(new Body());
            var body = mainPart.Document.Body!;

            body.Append(Paragraph("Отчет SmartUK", 30, true));
            body.Append(Paragraph(ReportTitle(reportType), 24, true));
            body.Append(Paragraph($"Дата формирования: {DisplayTime.Now:dd.MM.yyyy HH:mm}"));
            body.Append(Paragraph($"Период: {from:dd.MM.yyyy} - {to:dd.MM.yyyy}"));
            body.Append(Paragraph($"Квартира: {apartmentFilter}"));
            body.Append(Paragraph($"Тип приборов: {meterTypeFilter}"));
            body.Append(Paragraph(string.Empty));

            if (reportType == UtilityReportType.Consumption)
            {
                AddConsumptionReport(body, consumptionRows, summaryRows);
            }
            else
            {
                AddCriticalReport(body, criticalRows);
            }

            body.Append(new SectionProperties(
                new PageMargin
                {
                    Top = 720,
                    Right = 720,
                    Bottom = 720,
                    Left = 720
                }));
            mainPart.Document.Save();
        }

        return stream.ToArray();
    }

    private static void AddConsumptionReport(
        Body body,
        IReadOnlyList<ConsumptionReportRow> rows,
        IReadOnlyList<ConsumptionSummaryRow> summaryRows)
    {
        body.Append(Paragraph("Итоги по видам ресурсов", 22, true));
        body.Append(summaryRows.Count == 0
            ? Paragraph("Нет данных для расчета потребления за выбранный период.")
            : Table(
                ["Ресурс", "Ед. изм.", "Общий расход", "Приборов", "Показаний"],
                summaryRows.Select(x => new[]
                {
                    x.MeterType,
                    x.Unit,
                    FormatDecimal(x.TotalConsumption),
                    x.MeterCount.ToString(RuCulture),
                    x.ReadingsCount.ToString(RuCulture)
                })));

        body.Append(Paragraph(string.Empty));
        body.Append(Paragraph("Потребление по квартирам и приборам", 22, true));
        body.Append(rows.Count == 0
            ? Paragraph("Нет приборов, подходящих под выбранные фильтры.")
            : Table(
                [
                    "Квартира",
                    "Житель",
                    "Email",
                    "Прибор",
                    "Серийный номер",
                    "Начальное",
                    "Конечное",
                    "Расход",
                    "Статус"
                ],
                rows.Select(x => new[]
                {
                    x.ApartmentNumber,
                    x.Residents,
                    x.Emails,
                    x.MeterType,
                    x.SerialNumber,
                    FormatReading(x.FirstValue, x.FirstMeasuredAt, x.Unit),
                    FormatReading(x.LastValue, x.LastMeasuredAt, x.Unit),
                    x.Consumption is null ? "-" : $"{FormatDecimal(x.Consumption.Value)} {x.Unit}",
                    $"{x.MeterStatus.ToDisplayName()}; {x.QualitySummary}"
                })));
    }

    private static void AddCriticalReport(
        Body body,
        IReadOnlyList<CriticalReportRow> rows)
    {
        body.Append(Paragraph("Проблемные показания и приборы", 22, true));
        body.Append(rows.Count == 0
            ? Paragraph("За выбранный период проблемные показания и приборы не найдены.")
            : Table(
                [
                    "Квартира",
                    "Житель",
                    "Email",
                    "Прибор",
                    "Серийный номер",
                    "Проблема",
                    "Значение",
                    "Дата",
                    "Детали"
                ],
                rows.Select(x => new[]
                {
                    x.ApartmentNumber,
                    x.Residents,
                    x.Emails,
                    x.MeterType,
                    x.SerialNumber,
                    x.Problem,
                    x.Value is null ? "-" : $"{x.Value} {x.Unit}",
                    FormatDate(x.MeasuredAt),
                    x.Details
                })));
    }

    private static string ReportTitle(UtilityReportType reportType) => reportType switch
    {
        UtilityReportType.Consumption => "Отчет по потреблению коммунальных ресурсов",
        UtilityReportType.Critical => "Отчет по проблемным показаниям",
        _ => "Отчет"
    };

    private static Table Table(
        IReadOnlyList<string> headers,
        IEnumerable<IReadOnlyList<string>> rows)
    {
        var table = new Table(
            new TableProperties(
                new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
                new TableBorders(
                    new TopBorder { Val = BorderValues.Single, Size = 4 },
                    new BottomBorder { Val = BorderValues.Single, Size = 4 },
                    new LeftBorder { Val = BorderValues.Single, Size = 4 },
                    new RightBorder { Val = BorderValues.Single, Size = 4 },
                    new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4 },
                    new InsideVerticalBorder { Val = BorderValues.Single, Size = 4 })));

        table.Append(new TableRow(headers.Select(x => Cell(x, true))));
        foreach (var row in rows)
        {
            table.Append(new TableRow(row.Select(x => Cell(x, false))));
        }

        return table;
    }

    private static TableCell Cell(string text, bool isHeader)
    {
        var properties = new TableCellProperties(
            new TableCellMargin(
                new TopMargin { Width = "80", Type = TableWidthUnitValues.Dxa },
                new BottomMargin { Width = "80", Type = TableWidthUnitValues.Dxa },
                new LeftMargin { Width = "80", Type = TableWidthUnitValues.Dxa },
                new RightMargin { Width = "80", Type = TableWidthUnitValues.Dxa }));

        if (isHeader)
        {
            properties.Append(new Shading { Val = ShadingPatternValues.Clear, Fill = "E9EEF7" });
        }

        return new TableCell(properties, Paragraph(text, 18, isHeader));
    }

    private static Paragraph Paragraph(string text, int fontSize = 20, bool bold = false)
    {
        var runProperties = new RunProperties(new FontSize { Val = fontSize.ToString(CultureInfo.InvariantCulture) });
        if (bold)
        {
            runProperties.Append(new Bold());
        }

        return new Paragraph(
            new Run(
                runProperties,
                new Text(text) { Space = SpaceProcessingModeValues.Preserve }));
    }

    private static string FormatReading(decimal? value, DateTimeOffset? measuredAt, string unit)
    {
        if (value is null)
        {
            return "-";
        }

        return $"{FormatDecimal(value.Value)} {unit} ({FormatDate(measuredAt)})";
    }

    private static string FormatDecimal(decimal value) => value.ToString("0.###", RuCulture);

    private static string FormatDate(DateTimeOffset? value)
    {
        return value?.ToDisplayTime().ToString("dd.MM.yyyy HH:mm", RuCulture) ?? "-";
    }
}

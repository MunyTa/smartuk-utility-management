using UkManagement.Web.Domain;

namespace UkManagement.Web.Services.Reports;

public enum UtilityReportType
{
    Consumption = 1,
    Critical = 2
}

public sealed record ConsumptionReportRow(
    string ApartmentNumber,
    string BuildingAddress,
    string Residents,
    string Emails,
    string Phones,
    string MeterType,
    string SerialNumber,
    string ExternalDeviceId,
    string Unit,
    MeterStatus MeterStatus,
    decimal? FirstValue,
    DateTimeOffset? FirstMeasuredAt,
    decimal? LastValue,
    DateTimeOffset? LastMeasuredAt,
    decimal? Consumption,
    int ReadingsCount,
    int ProblemReadingsCount,
    string QualitySummary);

public sealed record ConsumptionSummaryRow(
    string MeterType,
    string Unit,
    decimal TotalConsumption,
    int MeterCount,
    int ReadingsCount);

public sealed record CriticalReportRow(
    string ApartmentNumber,
    string Residents,
    string Emails,
    string MeterType,
    string SerialNumber,
    string Problem,
    string? Value,
    string? Unit,
    DateTimeOffset? MeasuredAt,
    string Details);

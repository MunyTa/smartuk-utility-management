namespace UkManagement.Web.Domain;

public enum MeterType
{
    ColdWater = 1,
    HotWater = 2,
    Electricity = 3,
    Gas = 4,
    Heating = 5
}

public enum MeterStatus
{
    Online = 1,
    Warning = 2,
    Offline = 3
}

public enum ReadingQuality
{
    Normal = 1,
    Anomaly = 2,
    Invalid = 3
}

public enum NotificationChannel
{
    Email = 1,
    Sms = 2,
    Push = 3
}

public enum NotificationStatus
{
    Pending = 1,
    Sent = 2,
    Failed = 3
}

public enum ServiceRequestCategory
{
    Plumbing = 1,
    Electricity = 2,
    Cleaning = 3,
    Elevator = 4,
    Heating = 5,
    MeterReplacement = 6,
    Other = 7,
    MeterInstallation = 8
}

public enum ServiceRequestPriority
{
    Low = 1,
    Normal = 2,
    High = 3,
    Emergency = 4
}

public enum ServiceRequestStatus
{
    New = 1,
    InProgress = 2,
    WaitingResident = 3,
    Completed = 4,
    Cancelled = 5
}

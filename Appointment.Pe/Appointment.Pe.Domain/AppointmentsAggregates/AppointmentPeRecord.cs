using Appointment.Pe.CrossCutting;

namespace Appointment.Pe.Domain.AppointmentsAggregates;

public sealed class AppointmentPeRecord : AuditableEntity
{
    public Guid AppointmentId { get; private set; }
    public string InsuredId { get; private set; } = default!;
    public int ScheduleId { get; private set; }
    public string CountryISO { get; private set; } = default!;
    public string Status { get; private set; } = default!;
    public DateTime ProcessedAt { get; private set; }

    private AppointmentPeRecord() { }

    public static Result<AppointmentPeRecord> CreateProcessed(
        Guid appointmentId,
        string insuredId,
        int scheduleId,
        string countryISO,
        DateTime? nowUtc = null)
        => CreateInternal(appointmentId, insuredId, scheduleId, countryISO, AppointmentPeStatus.Processed, nowUtc);

    public static Result<AppointmentPeRecord> CreateCompleted(
        Guid appointmentId,
        string insuredId,
        int scheduleId,
        string countryISO,
        DateTime? nowUtc = null)
        => CreateInternal(appointmentId, insuredId, scheduleId, countryISO, AppointmentPeStatus.Completed, nowUtc);

    public static Result<AppointmentPeRecord> Restore(
        Guid appointmentId,
        string insuredId,
        int scheduleId,
        string countryISO,
        string status,
        DateTime createdDateUtc,
        DateTime processedAtUtc)
        => RestoreInternal(appointmentId, insuredId, scheduleId, countryISO, status, createdDateUtc, processedAtUtc);

    private static Result<AppointmentPeRecord> CreateInternal(
        Guid appointmentId,
        string insuredId,
        int scheduleId,
        string countryISO,
        string status,
        DateTime? nowUtc)
    {
        if (appointmentId == Guid.Empty)
        {
            return Result.Failure<AppointmentPeRecord>(AppointmentPeErrors.InvalidAppointmentId);
        }

        insuredId = (insuredId ?? string.Empty).Trim();
        if (insuredId.Length != 5 || insuredId.Any(c => !char.IsDigit(c)))
        {
            return Result.Failure<AppointmentPeRecord>(AppointmentPeErrors.InvalidInsuredId);
        }

        if (scheduleId <= 0)
        {
            return Result.Failure<AppointmentPeRecord>(AppointmentPeErrors.InvalidScheduleId);
        }

        countryISO = (countryISO ?? string.Empty).Trim().ToUpperInvariant();
        if (!string.Equals(countryISO, "PE", StringComparison.Ordinal))
        {
            return Result.Failure<AppointmentPeRecord>(AppointmentPeErrors.InvalidCountryISO);
        }

        if (!string.Equals(status, AppointmentPeStatus.Processed, StringComparison.Ordinal) &&
            !string.Equals(status, AppointmentPeStatus.Completed, StringComparison.Ordinal))
        {
            return Result.Failure<AppointmentPeRecord>(AppointmentPeErrors.InvalidStatus);
        }

        var utcNow = nowUtc ?? DateTime.UtcNow;

        var record = new AppointmentPeRecord
        {
            Id = appointmentId,
            AppointmentId = appointmentId,
            InsuredId = insuredId,
            ScheduleId = scheduleId,
            CountryISO = countryISO,
            Status = status,
            ProcessedAt = utcNow
        };

        record.AddCreateInfo(user: null, date: utcNow);

        return Result.Success(record);
    }

    private static Result<AppointmentPeRecord> RestoreInternal(
        Guid appointmentId,
        string insuredId,
        int scheduleId,
        string countryISO,
        string status,
        DateTime createdDateUtc,
        DateTime processedAtUtc)
    {
        var baseResult = CreateInternal(appointmentId, insuredId, scheduleId, countryISO, status, createdDateUtc);
        if (baseResult.IsFailure)
        {
            return Result.Failure<AppointmentPeRecord>(baseResult.Error);
        }

        if (createdDateUtc == default || processedAtUtc == default || processedAtUtc < createdDateUtc)
        {
            return Result.Failure<AppointmentPeRecord>(AppointmentPeErrors.InvalidTimestamps);
        }

        baseResult.Value.ProcessedAt = processedAtUtc;
        baseResult.Value.AddCreateInfo(user: null, date: createdDateUtc);

        return baseResult;
    }
}

public static class AppointmentPeStatus
{
    public const string Processed = "Processed";
    public const string Completed = "Completed";
}

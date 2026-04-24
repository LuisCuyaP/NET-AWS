using Appointment.Cl.CrossCutting;

namespace Appointment.Cl.Domain.AppointmentsAggregates;

public sealed class AppointmentClRecord : AuditableEntity
{
    public Guid AppointmentId { get; private set; }
    public string InsuredId { get; private set; } = default!;
    public int ScheduleId { get; private set; }
    public string CountryISO { get; private set; } = default!;
    public string Status { get; private set; } = default!;
    public DateTime ProcessedAt { get; private set; }

    private AppointmentClRecord() { }

    private AppointmentClRecord(Guid id) : base(id) { }

    public static Result<AppointmentClRecord> CreateProcessed(
        Guid appointmentId,
        string insuredId,
        int scheduleId,
        string countryISO,
        DateTime? nowUtc = null)
        => CreateInternal(appointmentId, insuredId, scheduleId, countryISO, AppointmentClStatus.Processed, nowUtc);

    public static Result<AppointmentClRecord> CreateCompleted(
        Guid appointmentId,
        string insuredId,
        int scheduleId,
        string countryISO,
        DateTime? nowUtc = null)
        => CreateInternal(appointmentId, insuredId, scheduleId, countryISO, AppointmentClStatus.Completed, nowUtc);

    public static Result<AppointmentClRecord> Restore(
        Guid appointmentId,
        string insuredId,
        int scheduleId,
        string countryISO,
        string status,
        DateTime createdDateUtc,
        DateTime processedAtUtc)
        => RestoreInternal(appointmentId, insuredId, scheduleId, countryISO, status, createdDateUtc, processedAtUtc);

    private static Result<AppointmentClRecord> CreateInternal(
        Guid appointmentId,
        string insuredId,
        int scheduleId,
        string countryISO,
        string status,
        DateTime? nowUtc)
    {
        if (appointmentId == Guid.Empty)
        {
            return Result.Failure<AppointmentClRecord>(AppointmentClErrors.InvalidAppointmentId);
        }

        insuredId = (insuredId ?? string.Empty).Trim();
        if (insuredId.Length != 5 || insuredId.Any(c => !char.IsDigit(c)))
        {
            return Result.Failure<AppointmentClRecord>(AppointmentClErrors.InvalidInsuredId);
        }

        if (scheduleId <= 0)
        {
            return Result.Failure<AppointmentClRecord>(AppointmentClErrors.InvalidScheduleId);
        }

        countryISO = (countryISO ?? string.Empty).Trim().ToUpperInvariant();
        if (!string.Equals(countryISO, "CL", StringComparison.Ordinal))
        {
            return Result.Failure<AppointmentClRecord>(AppointmentClErrors.InvalidCountryISO);
        }

        if (!string.Equals(status, AppointmentClStatus.Processed, StringComparison.Ordinal) &&
            !string.Equals(status, AppointmentClStatus.Completed, StringComparison.Ordinal))
        {
            return Result.Failure<AppointmentClRecord>(AppointmentClErrors.InvalidStatus);
        }

        var utcNow = nowUtc ?? DateTime.UtcNow;

        var record = new AppointmentClRecord(appointmentId)
        {
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

    private static Result<AppointmentClRecord> RestoreInternal(
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
            return Result.Failure<AppointmentClRecord>(baseResult.Error);
        }

        if (createdDateUtc == default || processedAtUtc == default || processedAtUtc < createdDateUtc)
        {
            return Result.Failure<AppointmentClRecord>(AppointmentClErrors.InvalidTimestamps);
        }

        baseResult.Value.ProcessedAt = processedAtUtc;
        baseResult.Value.AddCreateInfo(user: baseResult.Value.CreatedBy, date: createdDateUtc);

        return baseResult;
    }
}

public static class AppointmentClStatus
{
    public const string Processed = "Processed";
    public const string Completed = "Completed";
}

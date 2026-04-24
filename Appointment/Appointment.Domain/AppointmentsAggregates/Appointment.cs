using Appointment.CrossCutting;

namespace Appointment.Domain.AppointmentsAggregates;

public class Appointment : AuditableEntity
{
    public string InsuredId { get; private set; } = default!;
    public int ScheduleId { get; private set; }
    public string CountryISO { get; private set; } = default!;
    public string Status { get; private set; } = default!;

    private Appointment() { }

    public static Result<Appointment> Create(Guid id, string insuredId, int scheduleId, string countryISO)
    {
        if (id == Guid.Empty)
        {
            return Result.Failure<Appointment>(AppointmentErrors.InvalidId);
        }

        insuredId = (insuredId ?? string.Empty).Trim();
        if (insuredId.Length != 5 || insuredId.Any(c => !char.IsDigit(c)))
        {
            return Result.Failure<Appointment>(AppointmentErrors.InvalidInsuredId);
        }

        if (scheduleId <= 0)
        {
            return Result.Failure<Appointment>(AppointmentErrors.InvalidScheduleId);
        }

        countryISO = (countryISO ?? string.Empty).Trim().ToUpperInvariant();
        if (countryISO is not ("PE" or "CL"))
        {
            return Result.Failure<Appointment>(AppointmentErrors.InvalidCountryISO);
        }

        var appointment = new Appointment
        {
            Id = id,
            InsuredId = insuredId,
            ScheduleId = scheduleId,
            CountryISO = countryISO,
            Status = AppointmentStatus.Pending
        };

        appointment.AddCreateInfo(user: null, date: DateTime.UtcNow);

        return Result.Success(appointment);
    }

    public Result MarkAsCompleted()
    {
        if (!string.Equals(Status, AppointmentStatus.Pending, StringComparison.Ordinal))
        {
            return Result.Failure(AppointmentErrors.InvalidStatusTransition);
        }

        Status = AppointmentStatus.Completed;
        AddModifyInfo(user: null, date: DateTime.UtcNow);

        return Result.Success();
    }
}

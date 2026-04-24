namespace Appointment.Pe.Domain.AppointmentsAggregates;

public interface IAppointmentPeRepository
{
    Task<AppointmentPeRecord?> GetByAppointmentIdAsync(Guid appointmentId, CancellationToken cancellationToken = default);
    Task UpsertAsync(AppointmentPeRecord record, CancellationToken cancellationToken = default);
}

namespace Appointment.Cl.Domain.AppointmentsAggregates;

public interface IAppointmentClRepository
{
    Task<AppointmentClRecord?> GetByAppointmentIdAsync(Guid appointmentId, CancellationToken cancellationToken = default);
    Task UpsertAsync(AppointmentClRecord record, CancellationToken cancellationToken = default);
}

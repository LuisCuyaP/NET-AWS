namespace Appointment.Application.Abstractions.Messaging;

public interface IAppointmentEventPublisher
{
    Task PublishAppointmentRequestedAsync(Guid appointmentId, string insuredId, int scheduleId, string countryISO, string status, CancellationToken cancellationToken = default);
}

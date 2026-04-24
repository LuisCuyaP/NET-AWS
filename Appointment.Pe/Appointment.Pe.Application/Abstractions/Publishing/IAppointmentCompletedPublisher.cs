namespace Appointment.Pe.Application.Abstractions.Publishing;

public interface IAppointmentCompletedPublisher
{
    Task PublishAsync(AppointmentCompletedEvent @event, CancellationToken cancellationToken = default);
}

public sealed record AppointmentCompletedEvent(
    string EventType,
    Guid EventId,
    DateTime OccurredAt,
    Guid AppointmentId,
    string InsuredId,
    int ScheduleId,
    string CountryISO,
    string Status,
    string ProcessedBy);

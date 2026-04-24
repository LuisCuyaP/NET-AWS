namespace Appointment.Cl.Application.AppointmentsAggregates.ProcessClAppointment;

public sealed class ProcessClAppointmentMessage
{
    public string EventType { get; set; } = default!;
    public Guid EventId { get; set; }
    public DateTime OccurredAt { get; set; }
    public Guid AppointmentId { get; set; }
    public string InsuredId { get; set; } = default!;
    public int ScheduleId { get; set; }
    public string CountryISO { get; set; } = default!;
    public string Status { get; set; } = default!;
    public string Source { get; set; } = default!;
}

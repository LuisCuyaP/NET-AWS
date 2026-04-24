using Appointment.Application.Abstractions.Messaging;

namespace Appointment.Application.AppointmentsAggregates.RegisterAppointment;

public class RegisterAppointmentCommand : ICommand<RegisterAppointmentResponse>
{
    public string? InsuredId { get; set; }
    public int ScheduleId { get; set; }
    public string? CountryISO { get; set; }
}

public class RegisterAppointmentResponse
{
    public Guid AppointmentId { get; set; }
    public string Status { get; set; } = default!;
    public string Message { get; set; } = default!;
}

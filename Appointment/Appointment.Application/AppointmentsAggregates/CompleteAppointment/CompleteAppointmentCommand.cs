using Appointment.Application.Abstractions.Messaging;

namespace Appointment.Application.AppointmentsAggregates.CompleteAppointment;

public class CompleteAppointmentCommand : ICommand
{
    public CompleteAppointmentMessage Message { get; set; } = default!;
}

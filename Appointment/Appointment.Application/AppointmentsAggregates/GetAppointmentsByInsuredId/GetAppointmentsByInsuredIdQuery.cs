using Appointment.Application.Abstractions.Messaging;

namespace Appointment.Application.AppointmentsAggregates.GetAppointmentsByInsuredId;

public class GetAppointmentsByInsuredIdQuery : IQuery<GetAppointmentsByInsuredIdResponse>
{
    public string? InsuredId { get; set; }
}

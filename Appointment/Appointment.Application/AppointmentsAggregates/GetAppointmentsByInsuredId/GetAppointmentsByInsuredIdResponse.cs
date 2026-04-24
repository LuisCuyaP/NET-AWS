namespace Appointment.Application.AppointmentsAggregates.GetAppointmentsByInsuredId;

public class GetAppointmentsByInsuredIdResponse
{
    public List<GetAppointmentsByInsuredIdItem> Appointments { get; set; } = [];
}

public class GetAppointmentsByInsuredIdItem
{
    public Guid AppointmentId { get; set; }
    public string Status { get; set; } = default!;
}

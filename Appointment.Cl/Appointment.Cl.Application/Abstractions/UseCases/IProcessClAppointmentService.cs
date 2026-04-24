using Appointment.Cl.Application.AppointmentsAggregates.ProcessClAppointment;
using Appointment.Cl.CrossCutting;

namespace Appointment.Cl.Application.Abstractions.UseCases;

public interface IProcessClAppointmentService
{
    Task<Result> ProcessAsync(ProcessClAppointmentMessage message, CancellationToken cancellationToken = default);
}

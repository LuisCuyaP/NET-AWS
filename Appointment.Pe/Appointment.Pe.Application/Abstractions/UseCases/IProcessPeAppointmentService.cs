using Appointment.Pe.CrossCutting;
using Appointment.Pe.Application.AppointmentsAggregates.ProcessPeAppointment;

namespace Appointment.Pe.Application.Abstractions.UseCases;

public interface IProcessPeAppointmentService
{
    Task<Result> ProcessAsync(ProcessPeAppointmentMessage message, CancellationToken cancellationToken = default);
}

using Appointment.Domain.Abstractions.Persistence;

namespace Appointment.Domain.AppointmentsAggregates;

public interface IAppointmentRepository : IBaseRepository<Appointment>
{
}

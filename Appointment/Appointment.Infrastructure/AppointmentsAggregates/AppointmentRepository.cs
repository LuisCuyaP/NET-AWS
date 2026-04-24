using Appointment.Domain.AppointmentsAggregates;
using Appointment.Infrastructure.Abstractions.Persistence;
using Appointment.Infrastructure.Database;
using AppointmentEntity = Appointment.Domain.AppointmentsAggregates.Appointment;

namespace Appointment.Infrastructure.AppointmentsAggregates;

internal sealed class AppointmentRepository(ApplicationDbContext context)
    : BaseRepository<AppointmentEntity>(context), IAppointmentRepository
{
}

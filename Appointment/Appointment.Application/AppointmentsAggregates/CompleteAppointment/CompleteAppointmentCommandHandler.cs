using Appointment.Application.Abstractions.Messaging;
using Appointment.CrossCutting;
using Appointment.Domain.AppointmentsAggregates;
using AppointmentEntity = Appointment.Domain.AppointmentsAggregates.Appointment;
using Appointment.Domain.Database;
using Microsoft.EntityFrameworkCore;

namespace Appointment.Application.AppointmentsAggregates.CompleteAppointment;

internal sealed class CompleteAppointmentCommandHandler(
    IAppointmentRepository appointmentRepository,
    IUnitOfWork unitOfWork) : ICommandHandler<CompleteAppointmentCommand>
{
    public async Task<Result> Handle(CompleteAppointmentCommand request, CancellationToken cancellationToken)
    {
        Guid appointmentId = request.Message.AppointmentId;

        AppointmentEntity? appointment = await appointmentRepository
            .Queryable()
            .FirstOrDefaultAsync(a => a.Id == appointmentId, cancellationToken);

        if (appointment is null)
        {
            return Result.Failure(AppointmentErrors.NotFound);
        }

        Result completeResult = appointment.MarkAsCompleted();
        if (completeResult.IsFailure)
        {
            return completeResult;
        }

        appointmentRepository.Update(appointment);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

using Appointment.Application.Abstractions.Messaging;
using Appointment.CrossCutting;
using Appointment.Domain.AppointmentsAggregates;
using AppointmentEntity = Appointment.Domain.AppointmentsAggregates.Appointment;
using Appointment.Domain.Database;

namespace Appointment.Application.AppointmentsAggregates.RegisterAppointment;

internal sealed class RegisterAppointmentCommandHandler(
    IAppointmentRepository appointmentRepository,
    IUnitOfWork unitOfWork,
    IAppointmentEventPublisher appointmentEventPublisher) : ICommandHandler<RegisterAppointmentCommand, RegisterAppointmentResponse>
{
    public async Task<Result<RegisterAppointmentResponse>> Handle(RegisterAppointmentCommand request, CancellationToken cancellationToken)
    {
        var createResult = AppointmentEntity.Create(
            Guid.NewGuid(),
            request.InsuredId ?? string.Empty,
            request.ScheduleId,
            request.CountryISO ?? string.Empty);

        if (createResult.IsFailure)
        {
            return Result.Failure<RegisterAppointmentResponse>(createResult.Error);
        }

        AppointmentEntity appointment = createResult.Value;

        appointmentRepository.Add(appointment);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        try
        {
            await appointmentEventPublisher.PublishAppointmentRequestedAsync(
                appointment.Id,
                appointment.InsuredId,
                appointment.ScheduleId,
                appointment.CountryISO,
                appointment.Status,
                cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<RegisterAppointmentResponse>(Error.Problem("Appointments.EventPublishFailed", ex.Message));
        }

        var response = new RegisterAppointmentResponse
        {
            AppointmentId = appointment.Id,
            Status = appointment.Status,
            Message = "Appointment registrada correctamente."
        };

        return Result.Success(response);
    }
}

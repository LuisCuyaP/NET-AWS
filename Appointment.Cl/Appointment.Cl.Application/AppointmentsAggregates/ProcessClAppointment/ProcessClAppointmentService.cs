using Appointment.Cl.Application.Abstractions.Publishing;
using Appointment.Cl.Application.Abstractions.UseCases;
using Appointment.Cl.CrossCutting;
using Appointment.Cl.Domain.AppointmentsAggregates;
using FluentValidation;

namespace Appointment.Cl.Application.AppointmentsAggregates.ProcessClAppointment;

public sealed class ProcessClAppointmentService(
    IAppointmentClRepository repository,
    IAppointmentCompletedPublisher publisher,
    IValidator<ProcessClAppointmentMessage> validator) : IProcessClAppointmentService
{
    public async Task<Result> ProcessAsync(ProcessClAppointmentMessage message, CancellationToken cancellationToken = default)
    {
        var validation = await validator.ValidateAsync(message, cancellationToken);
        if (!validation.IsValid)
        {
            var errors = validation.Errors
                .Select(e => Error.Validation($"Validation.{e.PropertyName}", e.ErrorMessage))
                .ToArray();

            return Result.Failure(new ValidationError(errors));
        }

        var countryISO = (message.CountryISO ?? string.Empty).Trim().ToUpperInvariant();
        if (!string.Equals(countryISO, "CL", StringComparison.Ordinal))
        {
            return Result.Failure(AppointmentClErrors.InvalidCountryISO);
        }

        var nowUtc = DateTime.UtcNow;

        var recordResult = AppointmentClRecord.CreateCompleted(
            message.AppointmentId,
            message.InsuredId,
            message.ScheduleId,
            countryISO,
            nowUtc);

        if (recordResult.IsFailure)
        {
            return Result.Failure(recordResult.Error);
        }

        await repository.UpsertAsync(recordResult.Value, cancellationToken);

        var outputEvent = new AppointmentCompletedEvent(
            EventType: "AppointmentCompleted",
            EventId: Guid.NewGuid(),
            OccurredAt: nowUtc,
            AppointmentId: message.AppointmentId,
            InsuredId: message.InsuredId.Trim(),
            ScheduleId: message.ScheduleId,
            CountryISO: countryISO,
            Status: AppointmentClStatus.Completed,
            ProcessedBy: "appointment-cl");

        await publisher.PublishAsync(outputEvent, cancellationToken);

        return Result.Success();
    }
}

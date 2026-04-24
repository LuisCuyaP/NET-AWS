using Appointment.Pe.Application.Abstractions.Publishing;
using Appointment.Pe.Application.Abstractions.UseCases;
using Appointment.Pe.CrossCutting;
using Appointment.Pe.Domain.AppointmentsAggregates;
using FluentValidation;

namespace Appointment.Pe.Application.AppointmentsAggregates.ProcessPeAppointment;

public sealed class ProcessPeAppointmentService(
    IAppointmentPeRepository repository,
    IAppointmentCompletedPublisher publisher,
    IValidator<ProcessPeAppointmentMessage> validator) : IProcessPeAppointmentService
{
    public async Task<Result> ProcessAsync(ProcessPeAppointmentMessage message, CancellationToken cancellationToken = default)
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
        if (!string.Equals(countryISO, "PE", StringComparison.Ordinal))
        {
            return Result.Failure(AppointmentPeErrors.InvalidCountryISO);
        }

        var nowUtc = DateTime.UtcNow;

        var recordResult = AppointmentPeRecord.CreateCompleted(
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
            Status: AppointmentPeStatus.Completed,
            ProcessedBy: "appointment-pe");

        await publisher.PublishAsync(outputEvent, cancellationToken);

        return Result.Success();
    }
}

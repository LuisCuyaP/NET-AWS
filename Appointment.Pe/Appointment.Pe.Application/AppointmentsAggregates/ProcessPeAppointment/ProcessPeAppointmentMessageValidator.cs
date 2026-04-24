using FluentValidation;

namespace Appointment.Pe.Application.AppointmentsAggregates.ProcessPeAppointment;

public sealed class ProcessPeAppointmentMessageValidator : AbstractValidator<ProcessPeAppointmentMessage>
{
    public ProcessPeAppointmentMessageValidator()
    {
        RuleFor(x => x.EventType)
            .NotEmpty()
            .Equal("AppointmentRequested");

        RuleFor(x => x.EventId)
            .NotEmpty();

        RuleFor(x => x.OccurredAt)
            .NotEmpty();

        RuleFor(x => x.AppointmentId)
            .NotEmpty();

        RuleFor(x => x.InsuredId)
            .NotEmpty()
            .Must(x => x.Trim().Length == 5 && x.Trim().All(char.IsDigit))
            .WithMessage("insuredId must have exactly 5 digits.");

        RuleFor(x => x.ScheduleId)
            .GreaterThan(0);

        RuleFor(x => x.CountryISO)
            .NotEmpty()
            .Must(x => string.Equals((x ?? string.Empty).Trim().ToUpperInvariant(), "PE", StringComparison.Ordinal))
            .WithMessage("countryISO must be 'PE'.");

        RuleFor(x => x.Status)
            .NotEmpty();

        RuleFor(x => x.Source)
            .NotEmpty();
    }
}

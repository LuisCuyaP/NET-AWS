using FluentValidation;

namespace Appointment.Cl.Application.AppointmentsAggregates.ProcessClAppointment;

public sealed class ProcessClAppointmentMessageValidator : AbstractValidator<ProcessClAppointmentMessage>
{
    public ProcessClAppointmentMessageValidator()
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
            .Must(x => string.Equals((x ?? string.Empty).Trim().ToUpperInvariant(), "CL", StringComparison.Ordinal))
            .WithMessage("countryISO must be 'CL'.");

        RuleFor(x => x.Status)
            .NotEmpty();

        RuleFor(x => x.Source)
            .NotEmpty();
    }
}

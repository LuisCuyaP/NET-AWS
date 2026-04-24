using FluentValidation;

namespace Appointment.Application.AppointmentsAggregates.RegisterAppointment;

internal sealed class RegisterAppointmentCommandValidator : AbstractValidator<RegisterAppointmentCommand>
{
    public RegisterAppointmentCommandValidator()
    {
        RuleFor(x => x.InsuredId)
            .NotEmpty().WithMessage("El insuredId es obligatorio.")
            .Matches("^[0-9]{5}$").WithMessage("El insuredId debe tener exactamente 5 dígitos.");

        RuleFor(x => x.ScheduleId)
            .GreaterThan(0).WithMessage("El scheduleId debe ser mayor a 0.");

        RuleFor(x => x.CountryISO)
            .NotEmpty().WithMessage("El countryISO es obligatorio.")
            .Must(country =>
            {
                string normalized = (country ?? string.Empty).Trim().ToUpperInvariant();
                return normalized is "PE" or "CL";
            })
            .WithMessage("El countryISO solo puede ser PE o CL.");
    }
}

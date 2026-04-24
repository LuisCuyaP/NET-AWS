using Appointment.CrossCutting;

namespace Appointment.Domain.AppointmentsAggregates;

public static class AppointmentErrors
{
    public static Error InvalidId => Error.Failure("Appointments.InvalidId", "El id no puede ser vacío.");
    public static Error InvalidInsuredId => Error.Failure("Appointments.InvalidInsuredId", "El insuredId debe tener exactamente 5 dígitos.");
    public static Error InvalidScheduleId => Error.Failure("Appointments.InvalidScheduleId", "El scheduleId debe ser mayor a 0.");
    public static Error InvalidCountryISO => Error.Failure("Appointments.InvalidCountryISO", "El countryISO solo puede ser PE o CL.");
    public static Error InvalidStatusTransition => Error.Failure("Appointments.InvalidStatusTransition", "Transición de estado inválida.");
    public static Error NotFound => Error.NotFound("Appointments.NotFound", "Appointment no encontrada.");
}

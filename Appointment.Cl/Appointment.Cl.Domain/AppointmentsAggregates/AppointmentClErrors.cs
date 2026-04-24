using Appointment.Cl.CrossCutting;

namespace Appointment.Cl.Domain.AppointmentsAggregates;

public static class AppointmentClErrors
{
    public static Error InvalidAppointmentId => Error.Failure(
        "AppointmentCl.InvalidAppointmentId",
        "El appointmentId no puede ser vacío.");

    public static Error InvalidInsuredId => Error.Failure(
        "AppointmentCl.InvalidInsuredId",
        "El insuredId debe tener exactamente 5 dígitos.");

    public static Error InvalidScheduleId => Error.Failure(
        "AppointmentCl.InvalidScheduleId",
        "El scheduleId debe ser mayor a 0.");

    public static Error InvalidCountryISO => Error.Failure(
        "AppointmentCl.InvalidCountryISO",
        "El countryISO debe ser exactamente 'CL'.");

    public static Error InvalidStatus => Error.Failure(
        "AppointmentCl.InvalidStatus",
        "El status solo puede ser Processed o Completed.");

    public static Error InvalidTimestamps => Error.Failure(
        "AppointmentCl.InvalidTimestamps",
        "Los timestamps del record no son válidos.");

    public static Error NotFound => Error.NotFound(
        "AppointmentCl.NotFound",
        "Appointment CL record no encontrado.");
}

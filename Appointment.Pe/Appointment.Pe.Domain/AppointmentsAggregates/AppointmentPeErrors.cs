using Appointment.Pe.CrossCutting;

namespace Appointment.Pe.Domain.AppointmentsAggregates;

public static class AppointmentPeErrors
{
    public static Error InvalidAppointmentId => Error.Failure(
        "AppointmentPe.InvalidAppointmentId",
        "El appointmentId no puede ser vacío.");

    public static Error InvalidInsuredId => Error.Failure(
        "AppointmentPe.InvalidInsuredId",
        "El insuredId debe tener exactamente 5 dígitos.");

    public static Error InvalidScheduleId => Error.Failure(
        "AppointmentPe.InvalidScheduleId",
        "El scheduleId debe ser mayor a 0.");

    public static Error InvalidCountryISO => Error.Failure(
        "AppointmentPe.InvalidCountryISO",
        "El countryISO debe ser exactamente 'PE'.");

    public static Error InvalidStatus => Error.Failure(
        "AppointmentPe.InvalidStatus",
        "El status solo puede ser Processed o Completed.");

    public static Error InvalidTimestamps => Error.Failure(
        "AppointmentPe.InvalidTimestamps",
        "Los timestamps del record no son válidos.");

    public static Error NotFound => Error.NotFound(
        "AppointmentPe.NotFound",
        "Appointment PE record no encontrado.");
}

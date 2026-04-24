using Appointment.CrossCutting;

namespace Appointment.Domain.Accounts;

public static class UserErrors
{
    public static Error NotAuthorized => Error.Failure("Users.NotAuthorized", "Credenciales incorrectas.");
}

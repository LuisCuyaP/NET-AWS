
using System.Security.Claims;

namespace Appointment.Infrastructure.Abstractions.Authentication;

internal static class ClaimsPrincipalExtensions
{
    public static string? GetRegistrationId(this ClaimsPrincipal? principal)
    {
        return principal?.FindFirstValue(ClaimTypes.Sid);
    }

    public static string? GetRole(this ClaimsPrincipal? principal)
    {
        return principal?.FindFirstValue(ClaimTypes.Role);
    }
}

using Appointment.Application.Abstractions.Authentication;
using Microsoft.AspNetCore.Http;

namespace Appointment.Infrastructure.Abstractions.Authentication;

internal sealed class UserContext(IHttpContextAccessor httpContextAccessor) : IUserContext
{
    public string? RegistrationId => httpContextAccessor.HttpContext?.User.GetRegistrationId();
    public string? Role => httpContextAccessor.HttpContext?.User.GetRole();
}
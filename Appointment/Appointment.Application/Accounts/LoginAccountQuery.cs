using Appointment.Application.Abstractions.Messaging;

namespace Appointment.Application.Accounts;

public class LoginAccountQuery : IQuery<string>
{
    public string? UserName { get; set; }
    public string? Password { get; set; }
    public string? Role { get; set; }
    public string? RegistrationId { get; set; }
    public string? UserApplication { get; set; }
}

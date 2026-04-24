namespace Appointment.Domain.Accounts;

public class User
{
    public string UserName { get; private set; } = default!;
    public string Password { get; private set; } = default!;
    public string? Role { get; private set; } = default!;
    public string? RegistrationId { get; private set; }
    public string? UserApplication { get; private set; }
    public static User Create(string username, string password, string? role, string? registrationId, string? userApplication)
        => new()
        {
            UserName = username,
            Password = password,
            Role = role,
            RegistrationId = registrationId,
            UserApplication = userApplication
        };
}

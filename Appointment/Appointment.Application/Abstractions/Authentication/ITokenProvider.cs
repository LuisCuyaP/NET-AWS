using Appointment.Domain.Accounts;

namespace Appointment.Application.Abstractions.Authentication;

public interface ITokenProvider
{
    string Create(User usuario);
}

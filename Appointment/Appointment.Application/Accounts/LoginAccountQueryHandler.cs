using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Appointment.Application.Abstractions.Authentication;
using Appointment.Application.Abstractions.Messaging;
using Appointment.CrossCutting;
using Appointment.Domain.Accounts;

namespace Appointment.Application.Accounts;

internal sealed class LoginAccountQueryHandler(IConfiguration configuration, ITokenProvider tokenProvider, ILogger<LoginAccountQueryHandler> logger) : IQueryHandler<LoginAccountQuery, string>
{
    public Task<Result<string>> Handle(LoginAccountQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Password))
        {
            logger.LogWarning("Usuario no autorizado {username}.", request.UserName);
            return Task.FromResult(Result.Failure<string>(UserErrors.NotAuthorized));
        }

        string? expectedUser = configuration["Jwt:User"];
        string? expectedPassword = configuration["Jwt:Password"];

        if (!string.Equals(request.UserName, expectedUser, StringComparison.Ordinal) ||
            !string.Equals(request.Password, expectedPassword, StringComparison.Ordinal))
        {
            logger.LogWarning("Usuario no autorizado {username}.", request.UserName);
            return Task.FromResult(Result.Failure<string>(UserErrors.NotAuthorized));
        }

        User usuario = User.Create(request.UserName, request.Password, request.Role, request.RegistrationId, request.UserApplication);
        return Task.FromResult(Result.Success(tokenProvider.Create(usuario)));
    }
}

using FluentValidation;
using Microsoft.Extensions.Configuration;

namespace Appointment.Application.Accounts;

internal sealed class LoginAccountValidator : AbstractValidator<LoginAccountQuery>
{
    public LoginAccountValidator(IConfiguration configuration)
    {
        RuleFor(x => x.UserName).NotEmpty().WithMessage("El nombre de usuario es obligatorio.");
        RuleFor(x => x.Password).NotEmpty().WithMessage("La contraseña es obligatoria.");
    }
}

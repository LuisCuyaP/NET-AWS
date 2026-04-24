using System.Security.Claims;
using System.Text;
using Appointment.Application.Abstractions.Authentication;
using Appointment.Domain.Accounts;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Appointment.Infrastructure.Abstractions.Authentication;

internal sealed class TokenProvider(IConfiguration configuration) : ITokenProvider
{
    public string Create(User usuario)
    {
        string secretKey = configuration["Jwt:Key"] ?? throw new InvalidOperationException("Missing configuration key 'Jwt:Key'.");
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);
        List<Claim> claims = new()
        {
            new Claim(ClaimTypes.Sid, usuario.RegistrationId ?? usuario.UserApplication ?? string.Empty),
        };
        string[] roles = usuario.Role == null ? Array.Empty<string>() : usuario.Role.Split(',');
        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.Now.AddMinutes(configuration.GetValue<int>("Jwt:DurationInMinutes")),
            SigningCredentials = credentials,
            Issuer = configuration["Jwt:Issuer"],
            Audience = configuration["Jwt:Audience"],
        };
        var handler = new JsonWebTokenHandler();
        return handler.CreateToken(tokenDescriptor);
    }
}
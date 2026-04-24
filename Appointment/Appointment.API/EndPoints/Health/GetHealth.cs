using Appointment.Api.Extensions;

namespace Appointment.Api.EndPoints.Health;

internal sealed class GetHealth : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        // Endpoint liviano para el target group del ALB y para validaciones manuales.
        // No toca SQL Server ni AWS; solo confirma que el proceso HTTP está levantado.
        app.MapGet("/health", () => Results.Ok(new
            {
                status = "Healthy"
            }))
            .WithName("GetHealth")
            .WithTags("Health")
            .WithOpenApi();
    }
}

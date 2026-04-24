using Appointment.Api.Extensions;
using Appointment.Application.AppointmentsAggregates.RegisterAppointment;
using MediatR;

namespace Appointment.Api.EndPoints.Appointments;

internal sealed class RegisterAppointment : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/appointments", async (RegisterAppointmentCommand command, IMediator mediator, CancellationToken cancellationToken) =>
            {
                var result = await mediator.Send(command, cancellationToken);

                return result.Match(
                    onSuccess: Results.Ok,
                    onFailure: ApiResults.Problem);
            })
            .WithName("RegisterAppointment")
            .WithTags("Appointments")
            .WithOpenApi();
    }
}

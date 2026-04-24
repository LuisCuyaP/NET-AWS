using Appointment.Api.Extensions;
using Appointment.Application.AppointmentsAggregates.GetAppointmentsByInsuredId;
using MediatR;

namespace Appointment.Api.EndPoints.Appointments;

internal sealed class GetAppointmentsByInsuredId : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/appointments/{insuredId}", async (string insuredId, IMediator mediator, CancellationToken cancellationToken) =>
            {
                var query = new GetAppointmentsByInsuredIdQuery { InsuredId = insuredId };
                var result = await mediator.Send(query, cancellationToken);

                return result.Match(
                    onSuccess: Results.Ok,
                    onFailure: ApiResults.Problem);
            })
            .WithName("GetAppointmentsByInsuredId")
            .WithTags("Appointments")
            .WithOpenApi();
    }
}

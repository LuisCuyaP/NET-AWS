using Appointment.Application.Abstractions.Messaging;
using Appointment.CrossCutting;
using Appointment.Domain.AppointmentsAggregates;
using Microsoft.EntityFrameworkCore;

namespace Appointment.Application.AppointmentsAggregates.GetAppointmentsByInsuredId;

internal sealed class GetAppointmentsByInsuredIdQueryHandler(IAppointmentRepository appointmentRepository) : IQueryHandler<GetAppointmentsByInsuredIdQuery, GetAppointmentsByInsuredIdResponse>
{
    public async Task<Result<GetAppointmentsByInsuredIdResponse>> Handle(GetAppointmentsByInsuredIdQuery request, CancellationToken cancellationToken)
    {
        string insuredId = (request.InsuredId ?? string.Empty).Trim();

        var appointments = await appointmentRepository
            .Queryable()
            .Where(a => a.InsuredId == insuredId)
            .Select(a => new GetAppointmentsByInsuredIdItem
            {
                AppointmentId = a.Id,
                Status = a.Status
            })
            .ToListAsync(cancellationToken);

        return Result.Success(new GetAppointmentsByInsuredIdResponse { Appointments = appointments });
    }
}

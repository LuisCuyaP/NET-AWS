using Appointment.CrossCutting;
using MediatR;

namespace Appointment.Application.Abstractions.Messaging;

public interface IQuery<TResponse> : IRequest<Result<TResponse>>;
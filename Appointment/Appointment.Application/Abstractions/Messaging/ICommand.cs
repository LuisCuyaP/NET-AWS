using Appointment.CrossCutting;
using MediatR;

namespace Appointment.Application.Abstractions.Messaging;

public interface ICommand : IRequest<Result>, IBaseCommand;
public interface ICommand<TResponse> : IRequest<Result<TResponse>>, IBaseCommand;
using MassTransit;
using Appointment.Application.Abstractions.Messaging;

namespace Appointment.Infrastructure.Abstractions.Messaging;

internal sealed class MassTransitMessagePublisher(IPublishEndpoint publishEndpoint) : IMessagePublisher
{
    public Task PublishAsync<TMessage>(TMessage message, CancellationToken cancellationToken = default)
        where TMessage : class
        => publishEndpoint.Publish(message, cancellationToken);
}

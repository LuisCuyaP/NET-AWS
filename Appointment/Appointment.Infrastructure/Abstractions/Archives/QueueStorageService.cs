using Azure.Storage.Queues;
using Appointment.Application.Abstractions.Archives;
using System.Text.Json;

namespace Appointment.Infrastructure.Abstractions.Archives;

internal sealed class QueueStorageService : IQueueStorageService
{
    private readonly QueueClient _queueClient;
    public QueueStorageService(string connectionString, string queueName)
    {
        _queueClient = new QueueClient(connectionString, queueName);
    }

    public async Task SendMessageAsync(string message, CancellationToken cancellationToken = default)
    {
        await _queueClient.SendMessageAsync(message, cancellationToken);
    }

    public async Task SendMessageAsync<T>(T message, CancellationToken cancellationToken = default)
    {
        var jsonMessage = JsonSerializer.Serialize(message);
        var utf8Bytes = System.Text.Encoding.UTF8.GetBytes(jsonMessage);
        var base64Message = Convert.ToBase64String(utf8Bytes);
        await _queueClient.SendMessageAsync(base64Message, cancellationToken);
    }
}

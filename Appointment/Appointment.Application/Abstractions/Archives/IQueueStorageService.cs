namespace Appointment.Application.Abstractions.Archives;

public interface IQueueStorageService
{
    Task SendMessageAsync(string message, CancellationToken cancellationToken = default);
    Task SendMessageAsync<T>(T message, CancellationToken cancellationToken = default);
}

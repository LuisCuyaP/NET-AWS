using Appointment.Application.Abstractions.Notification;

namespace Appointment.Infrastructure.Abstractions.Notification;

internal sealed class NotificationService : INotificationService
{
    public Task SendNotificationAsync(string message)
    {        
        Console.WriteLine($"Notification sent: {message}");
        return Task.CompletedTask;
    }
    
}
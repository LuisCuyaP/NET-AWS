using System.Text.Json;
using Amazon.EventBridge;
using Amazon.EventBridge.Model;
using Appointment.Cl.Application.Abstractions.Publishing;

namespace Appointment.Cl.Infrastructure.Messaging;

internal sealed class EventBridgeAppointmentCompletedPublisher(
    IAmazonEventBridge eventBridge,
    EventBridgePublishingSettings settings) : IAppointmentCompletedPublisher
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task PublishAsync(AppointmentCompletedEvent @event, CancellationToken cancellationToken = default)
    {
        var detailJson = JsonSerializer.Serialize(@event, JsonOptions);

        var entry = new PutEventsRequestEntry
        {
            EventBusName = settings.BusName,
            Source = settings.Source,
            DetailType = settings.DetailType,
            Time = @event.OccurredAt,
            Detail = detailJson
        };

        var response = await eventBridge.PutEventsAsync(new PutEventsRequest
        {
            Entries = new List<PutEventsRequestEntry> { entry }
        }, cancellationToken);

        if (response.FailedEntryCount > 0)
        {
            var firstError = response.Entries.FirstOrDefault(e => !string.IsNullOrWhiteSpace(e.ErrorCode));
            throw new InvalidOperationException($"Failed to publish EventBridge event. {firstError?.ErrorCode}: {firstError?.ErrorMessage}");
        }
    }
}

internal sealed class EventBridgePublishingSettings
{
    public string BusName { get; }
    public string Source { get; }
    public string DetailType { get; }

    public EventBridgePublishingSettings(string busName, string source, string detailType)
    {
        BusName = string.IsNullOrWhiteSpace(busName) ? throw new ArgumentException("AWS:EventBridgeBusName is required.", nameof(busName)) : busName.Trim();
        Source = string.IsNullOrWhiteSpace(source) ? throw new ArgumentException("AWS:EventBridgeSource is required.", nameof(source)) : source.Trim();
        DetailType = string.IsNullOrWhiteSpace(detailType) ? throw new ArgumentException("AWS:EventBridgeDetailType is required.", nameof(detailType)) : detailType.Trim();
    }
}

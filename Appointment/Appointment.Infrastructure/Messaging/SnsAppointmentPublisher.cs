using System.Text.Json;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Appointment.Application.Abstractions.Messaging;

namespace Appointment.Infrastructure.Messaging;

internal sealed class SnsAppointmentPublisher(
    IAmazonSimpleNotificationService sns,
    IConfiguration configuration,
    ILogger<SnsAppointmentPublisher> logger) : IAppointmentEventPublisher
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task PublishAppointmentRequestedAsync(
        Guid appointmentId,
        string insuredId,
        int scheduleId,
        string countryISO,
        string status,
        CancellationToken cancellationToken = default)
    {
        // Este publisher implementa el primer salto de la arquitectura event-driven:
        // Appointment.Api -> SNS Topic -> SQS por país.
        // El ARN del topic se inyecta por configuración para no acoplar el código
        // a un recurso fijo de AWS.
        string topicArn = configuration["AWS:Sns:AppointmentTopicArn"] ?? string.Empty;
        if (string.IsNullOrWhiteSpace(topicArn))
        {
            throw new InvalidOperationException("Missing configuration key 'AWS:Sns:AppointmentTopicArn'.");
        }

        var nowUtc = DateTime.UtcNow;
        var normalizedCountryIso = (countryISO ?? string.Empty).Trim().ToUpperInvariant();

        // El body del evento se mantiene alineado con el contrato validado
        // por las Lambdas de PE y CL.
        //
        // Importante:
        // Las Lambdas deserializan el payload esperando la propiedad `eventType`.
        // El atributo de mensaje `eventName` puede seguir existiendo para filtros
        // y observabilidad, pero el body de negocio debe usar `eventType`.
        var payload = new
        {
            eventType = "AppointmentRequested",
            eventId = Guid.NewGuid(),
            occurredAt = nowUtc,
            appointmentId,
            insuredId,
            scheduleId,
            countryISO = normalizedCountryIso,
            status,
            source = "appointment-api"
        };

        string messageJson = JsonSerializer.Serialize(payload, JsonOptions);

        try
        {
            var request = new PublishRequest
            {
                TopicArn = topicArn,
                Message = messageJson,
                MessageAttributes = new Dictionary<string, MessageAttributeValue>
                {
                    // eventName ayuda a observabilidad y trazabilidad del dominio.
                    ["eventName"] = new MessageAttributeValue
                    {
                        DataType = "String",
                        StringValue = "AppointmentRequested"
                    },
                    // countryISO es crítico porque SNS lo usa para aplicar la
                    // filter policy y enrutar a la cola correcta.
                    ["countryISO"] = new MessageAttributeValue
                    {
                        DataType = "String",
                        StringValue = normalizedCountryIso
                    }
                }
            };

            PublishResponse response = await sns.PublishAsync(request, cancellationToken);

            logger.LogInformation(
                "Published AppointmentRequested to SNS. AppointmentId={AppointmentId} CountryISO={CountryISO} MessageId={MessageId}",
                appointmentId,
                normalizedCountryIso,
                response.MessageId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed publishing AppointmentRequested to SNS. AppointmentId={AppointmentId}", appointmentId);
            throw;
        }
    }
}

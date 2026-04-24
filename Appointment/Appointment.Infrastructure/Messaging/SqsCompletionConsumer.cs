using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Appointment.Application.AppointmentsAggregates.CompleteAppointment;

namespace Appointment.Infrastructure.Messaging;

internal sealed class SqsCompletionConsumer(
    IAmazonSQS sqs,
    IConfiguration configuration,
    ILogger<SqsCompletionConsumer> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    internal sealed record CompletionQueueItem(string ReceiptHandle, string MessageId, CompleteAppointmentMessage Message);

    public async Task<IReadOnlyList<CompletionQueueItem>> ReceiveAsync(CancellationToken cancellationToken)
    {
        // Este consumer representa el último tramo del flujo:
        // EventBridge -> appointment-completion -> Appointment.Api worker.
        string queueUrl = configuration["AWS:Sqs:CompletionQueueUrl"] ?? string.Empty;
        if (string.IsNullOrWhiteSpace(queueUrl))
        {
            throw new InvalidOperationException("Missing configuration key 'AWS:Sqs:CompletionQueueUrl'.");
        }

        int maxMessages = int.TryParse(configuration["AWS:Sqs:CompletionMaxMessages"], out int parsedMax) ? Math.Clamp(parsedMax, 1, 10) : 10;
        int waitTimeSeconds = int.TryParse(configuration["AWS:Sqs:CompletionWaitTimeSeconds"], out int parsedWait) ? Math.Clamp(parsedWait, 0, 20) : 20;

        var request = new ReceiveMessageRequest
        {
            QueueUrl = queueUrl,
            MaxNumberOfMessages = maxMessages,
            WaitTimeSeconds = waitTimeSeconds
        };

        ReceiveMessageResponse response = await sqs.ReceiveMessageAsync(request, cancellationToken);

        if (response.Messages.Count == 0)
        {
            return Array.Empty<CompletionQueueItem>();
        }

        var items = new List<CompletionQueueItem>(response.Messages.Count);
        foreach (Message raw in response.Messages)
        {
            // La cola puede recibir distintos envelopes según el origen
            // o la configuración del target. Por eso no asumimos un único shape.
            CompleteAppointmentMessage? completion = TryParseCompletion(raw.Body);

            if (completion is null)
            {
                logger.LogWarning(
                    "Skipping SQS message due to invalid body. MessageId={MessageId}",
                    raw.MessageId);
                continue;
            }

            items.Add(new CompletionQueueItem(raw.ReceiptHandle, raw.MessageId, completion));
        }

        return items;
    }

    public async Task DeleteAsync(string receiptHandle, CancellationToken cancellationToken)
    {
        string queueUrl = configuration["AWS:Sqs:CompletionQueueUrl"] ?? string.Empty;
        if (string.IsNullOrWhiteSpace(queueUrl))
        {
            throw new InvalidOperationException("Missing configuration key 'AWS:Sqs:CompletionQueueUrl'.");
        }

        await sqs.DeleteMessageAsync(new DeleteMessageRequest
        {
            QueueUrl = queueUrl,
            ReceiptHandle = receiptHandle
        }, cancellationToken);
    }

    private CompleteAppointmentMessage? TryParseCompletion(string body)
    {
        try
        {
            // Primer intento: el mensaje ya viene directo con el contrato final.
            var direct = JsonSerializer.Deserialize<CompleteAppointmentMessage>(body, JsonOptions);
            if (direct?.AppointmentId != Guid.Empty)
            {
                return direct;
            }
        }
        catch
        {
            // ignore and attempt SNS envelope
        }

        try
        {
            using JsonDocument doc = JsonDocument.Parse(body);

            // Segundo intento: envelope estilo SNS con el body real dentro de Message.
            if (doc.RootElement.TryGetProperty("Message", out JsonElement messageElement) &&
                messageElement.ValueKind == JsonValueKind.String)
            {
                string? inner = messageElement.GetString();
                if (!string.IsNullOrWhiteSpace(inner))
                {
                    var innerMsg = JsonSerializer.Deserialize<CompleteAppointmentMessage>(inner, JsonOptions);
                    if (innerMsg?.AppointmentId != Guid.Empty)
                    {
                        return innerMsg;
                    }
                }               
            }

            // Tercer intento: envelope de EventBridge, donde el payload de negocio
            // viaja dentro de "detail". Este es el caso esperado en tu fase actual.
            if (doc.RootElement.TryGetProperty("detail", out JsonElement detailElement) &&
               detailElement.ValueKind == JsonValueKind.Object)
            {
                var detailMsg = JsonSerializer.Deserialize<CompleteAppointmentMessage>(
                    detailElement.GetRawText(),
                    JsonOptions);

                if (detailMsg?.AppointmentId != Guid.Empty)
                {
                    return detailMsg;
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Unable to parse completion message body.");
        }

        return null;
    }
}

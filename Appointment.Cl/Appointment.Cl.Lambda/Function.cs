using System.Text.Json;
using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using Appointment.Cl.Application.Abstractions.UseCases;
using Appointment.Cl.Application.AppointmentsAggregates.ProcessClAppointment;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace Appointment.Cl.Lambda;

public class Function
{
    // Construimos el contenedor de DI una sola vez por runtime de Lambda
    // para reutilizar clientes AWS y servicios entre invocaciones.
    private static readonly Lazy<IServiceProvider> ServiceProvider = new(DependencyInjection.BuildServiceProvider);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<SQSBatchResponse> FunctionHandler(SQSEvent sqsEvent, ILambdaContext context)
    {
        using var scope = ServiceProvider.Value.CreateScope();
        var useCase = scope.ServiceProvider.GetRequiredService<IProcessClAppointmentService>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Function>>();
        var cancellationToken = CancellationToken.None;

        logger.LogInformation(
            "SQS invocation started. RequestId={RequestId} Records={RecordCount}",
            context.AwsRequestId,
            sqsEvent.Records?.Count ?? 0);

        var failures = new List<SQSBatchResponse.BatchItemFailure>();

        if (sqsEvent.Records is null || sqsEvent.Records.Count == 0)
        {
            return new SQSBatchResponse { BatchItemFailures = failures };
        }

        foreach (var record in sqsEvent.Records)
        {
            try
            {
                logger.LogInformation(
                    "Processing SQS message. MessageId={MessageId} EventSourceArn={EventSourceArn}",
                    record.MessageId,
                    record.EventSourceArn);

                // El mensaje llega desde SQS como JSON plano publicado originalmente
                // por Appointment.Api hacia SNS y enrutado por filtro de país.
                var message = JsonSerializer.Deserialize<ProcessClAppointmentMessage>(record.Body, JsonOptions);
                if (message is null)
                {
                    throw new InvalidOperationException("Message body could not be deserialized.");
                }

                // El caso de uso encapsula la validación de countryISO,
                // persistencia en DynamoDB y publicación del AppointmentCompleted.
                var result = await useCase.ProcessAsync(message, cancellationToken);
                if (result.IsFailure)
                {
                    logger.LogWarning(
                        "Use case failed. MessageId={MessageId} ErrorCode={ErrorCode} ErrorDescription={ErrorDescription}",
                        record.MessageId,
                        result.Error.Code,
                        result.Error.Description);

                    failures.Add(new SQSBatchResponse.BatchItemFailure { ItemIdentifier = record.MessageId });
                    continue;
                }

                logger.LogInformation("Message processed successfully. MessageId={MessageId}", record.MessageId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing SQS message. MessageId={MessageId}", record.MessageId);

                // ReportBatchItemFailures permite reintentar solo los mensajes fallidos
                // del lote, en vez de reprocesar todos los que ya salieron bien.
                failures.Add(new SQSBatchResponse.BatchItemFailure { ItemIdentifier = record.MessageId });
            }
        }

        logger.LogInformation(
            "SQS invocation finished. RequestId={RequestId} Failed={FailedCount}",
            context.AwsRequestId,
            failures.Count);

        return new SQSBatchResponse { BatchItemFailures = failures };
    }
}

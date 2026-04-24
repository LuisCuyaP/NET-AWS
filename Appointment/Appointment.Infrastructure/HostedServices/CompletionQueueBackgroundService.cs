using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Appointment.Application.AppointmentsAggregates.CompleteAppointment;
using Appointment.CrossCutting;
using Appointment.Infrastructure.Messaging;

namespace Appointment.Infrastructure.HostedServices;

internal sealed class CompletionQueueBackgroundService(
    SqsCompletionConsumer consumer,
    IMediator mediator,
    IConfiguration configuration,
    ILogger<CompletionQueueBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Este worker corre dentro del mismo proceso de la API.
        // Su responsabilidad es leer la cola de completion y traducir ese evento
        // en una actualización del estado en SQL Server.
        TimeSpan idleDelay = TimeSpan.FromSeconds(
            int.TryParse(configuration["AWS:Sqs:CompletionIdleDelaySeconds"], out int delay) ? Math.Clamp(delay, 1, 60) : 2);

        logger.LogInformation("CompletionQueueBackgroundService started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                IReadOnlyList<SqsCompletionConsumer.CompletionQueueItem> items = await consumer.ReceiveAsync(stoppingToken);

                if (items.Count == 0)
                {
                    await Task.Delay(idleDelay, stoppingToken);
                    continue;
                }

                foreach (var item in items)
                {
                    Result result;
                    try
                    {
                        // Reutilizamos el flujo CQRS/MediatR de la solución en vez
                        // de escribir lógica transaccional directamente en el worker.
                        result = await mediator.Send(new CompleteAppointmentCommand { Message = item.Message }, stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error executing CompleteAppointmentCommand. MessageId={MessageId}", item.MessageId);
                        continue;
                    }

                    if (result.IsSuccess)
                    {
                        // Solo eliminamos de SQS cuando la actualización en SQL fue exitosa.
                        // Así conservamos semántica at-least-once del lado del consumer.
                        await consumer.DeleteAsync(item.ReceiptHandle, stoppingToken);
                        logger.LogInformation("Completion processed and deleted from SQS. MessageId={MessageId} AppointmentId={AppointmentId}", item.MessageId, item.Message.AppointmentId);
                    }
                    else
                    {
                        logger.LogWarning(
                            "Completion processing failed. MessageId={MessageId} AppointmentId={AppointmentId} ErrorCode={ErrorCode} Error={ErrorDescription}",
                            item.MessageId,
                            item.Message.AppointmentId,
                            result.Error.Code,
                            result.Error.Description);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "CompletionQueueBackgroundService loop failure.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        logger.LogInformation("CompletionQueueBackgroundService stopped.");
    }
}

using Amazon.DynamoDBv2;
using Amazon.EventBridge;
using Amazon.Extensions.NETCore.Setup;
using Appointment.Cl.Application.Abstractions.Publishing;
using Appointment.Cl.Domain.AppointmentsAggregates;
using Appointment.Cl.Infrastructure.AppointmentsAggregates;
using Appointment.Cl.Infrastructure.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Appointment.Cl.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // La Lambda usa el bloque AWS del appsettings o variables de entorno
        // para resolver región y credenciales mediante el SDK oficial.
        var awsOptions = configuration.GetAWSOptions();

        services.AddDefaultAWSOptions(awsOptions);
        services.AddAWSService<IAmazonDynamoDB>();
        services.AddAWSService<IAmazonEventBridge>();

        // Tabla DynamoDB destino donde quedará persistida la cita procesada.
        var tableName = configuration["AWS:ClAppointmentsTableName"];
        services.AddSingleton(new DynamoDbTableSettings(tableName ?? string.Empty));

        // Configuración del evento final que esta Lambda publica a EventBridge
        // una vez que el procesamiento del mensaje terminó correctamente.
        var busName = configuration["AWS:EventBridgeBusName"];
        var source = configuration["AWS:EventBridgeSource"];
        var detailType = configuration["AWS:EventBridgeDetailType"];
        services.AddSingleton(new EventBridgePublishingSettings(busName ?? string.Empty, source ?? string.Empty, detailType ?? string.Empty));

        // Repositorio: persistencia en DynamoDB.
        // Publisher: emisión del AppointmentCompleted al bus de eventos.
        services.AddScoped<IAppointmentClRepository, AppointmentClRepository>();
        services.AddScoped<IAppointmentCompletedPublisher, EventBridgeAppointmentCompletedPublisher>();

        return services;
    }
}

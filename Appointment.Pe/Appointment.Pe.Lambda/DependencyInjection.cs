using Appointment.Pe.Application.AppointmentsAggregates.ProcessPeAppointment;
using Appointment.Pe.Application.Abstractions.UseCases;
using Appointment.Pe.Infrastructure;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Appointment.Pe.Lambda;

internal static class DependencyInjection
{
    public static ServiceProvider BuildServiceProvider()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();

        var services = new ServiceCollection();

        services.AddSingleton<IConfiguration>(configuration);

        services.AddLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddConsole();
        });

        services.AddValidatorsFromAssemblyContaining<ProcessPeAppointmentMessageValidator>();
        services.AddScoped<IProcessPeAppointmentService, ProcessPeAppointmentService>();

        services.AddInfrastructure(configuration);

        return services.BuildServiceProvider();
    }
}

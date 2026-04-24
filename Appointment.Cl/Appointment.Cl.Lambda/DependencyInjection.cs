using Appointment.Cl.Application.Abstractions.UseCases;
using Appointment.Cl.Application.AppointmentsAggregates.ProcessClAppointment;
using Appointment.Cl.Infrastructure;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Appointment.Cl.Lambda;

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

        services.AddValidatorsFromAssemblyContaining<ProcessClAppointmentMessageValidator>();
        services.AddScoped<IProcessClAppointmentService, ProcessClAppointmentService>();

        services.AddInfrastructure(configuration);

        return services.BuildServiceProvider();
    }
}

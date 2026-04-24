using System.Text;
using Azure.Monitor.OpenTelemetry.Exporter;
using Appointment.Application.Abstractions.AI;
using Appointment.Application.Abstractions.Archives;
using Appointment.Application.Abstractions.Authentication;
using Appointment.Application.Abstractions.Data;
using Appointment.Domain.Database;
using Appointment.Infrastructure.Abstractions.AI;
using Appointment.Infrastructure.Abstractions.Archives;
using Appointment.Infrastructure.Abstractions.Authentication;
using Appointment.Infrastructure.Abstractions.Data;
using Appointment.Infrastructure.Database;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Trace;
using Azure.Identity;
using Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider;
using Microsoft.Data.SqlClient;
using System.Text.Json;
using Refit;
using Appointment.Application.Abstractions.Notification;
using Appointment.Infrastructure.Abstractions.Notification;
using MassTransit;
using System.Reflection;
using Appointment.Application.Abstractions.Messaging;
using Appointment.Infrastructure.Abstractions.Messaging;
using Appointment.Application.Abstractions.Caching;
using Appointment.Infrastructure.Abstractions.Caching;
using Appointment.Domain.AppointmentsAggregates;
using Appointment.Infrastructure.AppointmentsAggregates;
using Amazon;
using Amazon.SimpleNotificationService;
using Amazon.SQS;
using Appointment.Infrastructure.HostedServices;
using Appointment.Infrastructure.Messaging;

namespace Appointment.Infrastructure;

public static class DependencyInjection
{

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration, IWebHostEnvironment environment)
    {
        services
            .AddDatabase(configuration, environment)
            .AddRepositories()
            .AddAwsMessaging(configuration)
            .AddAuthenticationInternal(configuration);
            //.AddObservability(configuration)
        return services;
    }

    private static IServiceCollection AddAwsMessaging(this IServiceCollection services, IConfiguration configuration)
    {
        string regionName = configuration["AWS:Region"] ?? "us-east-1";
        RegionEndpoint region = RegionEndpoint.GetBySystemName(regionName);

        services.AddSingleton<IAmazonSimpleNotificationService>(_ => new AmazonSimpleNotificationServiceClient(region));
        services.AddSingleton<IAmazonSQS>(_ => new AmazonSQSClient(region));

        services.AddScoped<IAppointmentEventPublisher, SnsAppointmentPublisher>();
        services.AddScoped<SqsCompletionConsumer>();

        services.AddHostedService<CompletionQueueBackgroundService>();
        return services;
    }


    private static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration, IWebHostEnvironment environment)
    {
        string sqlConnectionString = configuration["AppointmentConnectionString"] ?? throw new ArgumentNullException(nameof(configuration));

        services.AddDbContext<ApplicationDbContext>((sp, options) =>
        {            
            options.UseSqlServer(sqlConnectionString).UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
            var interceptor = sp.GetRequiredService<AuditoriaInterceptor>();
            options.AddInterceptors(interceptor);
        });
/* 
        SqlColumnEncryptionAzureKeyVaultProvider azureKeyVaultProvider = new(new ClientSecretCredential(configuration["AzureAD:TenantId"],
                                                                        configuration["AzureAD:ClientId"],
                                                                        configuration["AzureAD:ClientSecret"]));

        Dictionary<string, SqlColumnEncryptionKeyStoreProvider> providers = new(
            capacity: 1,
            comparer: StringComparer.OrdinalIgnoreCase)
        {
            { SqlColumnEncryptionAzureKeyVaultProvider.ProviderName, azureKeyVaultProvider }
        };
        SqlConnection.RegisterColumnEncryptionKeyStoreProviders(providers); */

        return services;
    }

    private static IServiceCollection AddRepositories(this IServiceCollection services)
    {        
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<AuditoriaInterceptor>();
        services.AddScoped<IAppointmentRepository, AppointmentRepository>();
        return services;
    }

    private static IServiceCollection AddServices(this IServiceCollection services)
    {        
        services.AddSingleton<INotificationService, NotificationService>();        
        services.AddScoped<IMessagePublisher, MassTransitMessagePublisher>();
        return services;
    }

    private static IServiceCollection AddCache(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = configuration["Redis"];
        });

        services.AddSingleton<ICacheService, DistributedCacheService>();
        return services;
    }

    public static IServiceCollection AddMessageBroker(this IServiceCollection services, IConfiguration configuration, Assembly? assembly = null)
    {
        services.AddMassTransit(config =>
        {
            config.SetKebabCaseEndpointNameFormatter();

            if (assembly != null)
                config.AddConsumers(assembly);

            config.UsingRabbitMq((context, configurator) =>
            {
                configurator.Host(new Uri(configuration["MessageBroker:Host"]!), host =>
                {
                    host.Username(configuration["MessageBroker:UserName"]);
                    host.Password(configuration["MessageBroker:Password"]);
                });
                configurator.ConfigureEndpoints(context);
            });
        });

        return services;
    }


    private static IServiceCollection AddQueue(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IQueueStorageService>(provider =>
        {
            var connectionString = configuration["ConnectionString"] ?? throw new ArgumentNullException(nameof(configuration));
            var queueName = configuration["AzureQueue:QueueName"] ?? throw new ArgumentNullException(nameof(configuration));
            return new QueueStorageService(connectionString, queueName);
        });
        return services;
    }

    private static IServiceCollection AddStorage(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IBlobStorageService>(provider =>
        {
            var connectionString = configuration["BlobStorage:ConnectionString"] ?? throw new ArgumentNullException(nameof(configuration));
            var containerName = configuration["BlobStorage:ContainerName"] ?? throw new ArgumentNullException(nameof(configuration));
            return new BlobStorageService(connectionString, containerName);
        });
        return services;
    }

    private static IServiceCollection AddAuthenticationInternal(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(o =>
            {
                o.RequireHttpsMetadata = false;
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:Key"]!)),
                    ValidIssuer = configuration["Jwt:Issuer"],
                    ValidAudience = configuration["Jwt:Audience"],
                    ClockSkew = TimeSpan.Zero,
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true
                };
            });

        services.AddHttpContextAccessor();
        services.AddAuthorization();
        services.AddScoped<IUserContext, UserContext>();
        services.AddSingleton<ITokenProvider, TokenProvider>();
        return services;
    }

    private static IServiceCollection AddObservability(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionApplicationInsights = configuration["APINConnectionString"];                
        services.AddApplicationInsightsTelemetry(options => options.ConnectionString = connectionApplicationInsights)
            .AddOpenTelemetry()
            .WithTracing(tracing =>
            {
                tracing.AddAzureMonitorTraceExporter(o =>
                {
                    o.ConnectionString = connectionApplicationInsights;
                });
                tracing.AddAspNetCoreInstrumentation();
                tracing.AddHttpClientInstrumentation();
            });

        return services;
       
    }

    public static async Task<WebApplication> UseEnsureCreatedDatabaseAsync(this WebApplication builder)
    {
        bool ensureCreatedOnStartup =
            builder.Environment.IsDevelopment() ||
            bool.TryParse(builder.Configuration["Database:EnsureCreatedOnStartup"], out bool configuredEnsureCreated) &&
            configuredEnsureCreated;

        if (!ensureCreatedOnStartup)
        {
            return builder;
        }

        using var scoped = builder.Services.CreateScope();
        var context = scoped.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // EnsureCreated es suficiente aquí porque el modelo es pequeño
        // y no hay migraciones mantenidas en el workspace para esta solución.
        await context.Database.EnsureCreatedAsync();

        return builder;
    }
    private static IServiceCollection AddEncryption(this IServiceCollection services)
    {
        services.AddSingleton<IEncryptionService, EncryptionService>();
        return services;
    }

    private static IServiceCollection AddAzureAI(this IServiceCollection services)
    {
        // Register the executor; implementation should handle missing configuration at runtime.
        services.AddSingleton<IAzureAIPromptExecutor, AzureAIPromptExecutor>();
        return services;
    }

}

using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Identity;
using Azure.Security.KeyVault.Keys;
using Azure.Security.KeyVault.Secrets;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.OpenApi.Models;
using Serilog;

namespace Appointment.Api;

public static class DependencyInjection
{
    #region Configuration
    public static WebApplicationBuilder ConfigureAppointmentApi(this WebApplicationBuilder builder)
    {
        builder
            .AddConfigureCors()
            .AddForwardedHeadersConfiguration()
            .AddSerilog();
            //.AddServicesAzureKeyVault();

        return builder;
    }

    private static WebApplicationBuilder AddServicesAzureKeyVault(this WebApplicationBuilder builder)
    {
        if (!builder.Environment.IsDevelopment())
        {
            var connectionAzureKeyVault = $"https://{builder.Configuration["Vault"]}.vault.azure.net/";

            builder.Configuration.AddAzureKeyVault(new SecretClient(new Uri(connectionAzureKeyVault), new DefaultAzureCredential()), new KeyVaultSecretManager());


            #region JwtKey

            var client = new KeyClient(new Uri(connectionAzureKeyVault),new DefaultAzureCredential());

            builder.Configuration["JwtKey"] = client.GetKey("JwtKey")?.Value.Properties.Version ?? string.Empty;
            
            #endregion

        }

        return builder;
    }

    private static WebApplicationBuilder AddForwardedHeadersConfiguration(this WebApplicationBuilder builder)
    {
        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            // El contenedor no ve directamente al cliente.
            // El ALB reenvía el esquema y la IP original por headers estándar.
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

            // En cloud el proxy puede variar y no siempre estará en estas listas.
            // Limpiamos ambas colecciones para aceptar los headers reenviados
            // del balanceador que pondremos en Fargate.
            options.KnownNetworks.Clear();
            options.KnownProxies.Clear();
        });

        return builder;
    }

    private static WebApplicationBuilder AddConfigureCors(this WebApplicationBuilder builder)
    {
        var domains = builder.Configuration["DomainsAllowsCors"]?.Split(";") ?? [];
        domains = [.. domains.Where(x => x.Length > 0)];
        builder.Services.AddCors(options =>
        {
            options.AddPolicy("AllowSpecificOrigins", 
                builder =>
                {
                    if (domains.Length != 0)
                        builder.WithOrigins(domains).AllowAnyHeader().AllowAnyMethod();
                });
        });

        return builder;
    }

    private static WebApplicationBuilder AddSerilog(this WebApplicationBuilder builder)
    {
        builder.Host.UseSerilog((context, configuration) =>
            configuration.ReadFrom.Configuration(context.Configuration));
        return builder;
    }


    #endregion

    #region Add Presentation

    public static IServiceCollection AddPresentation(this IServiceCollection services)
    {
        services.AddSwagger();
        return services;
    }

    private static IServiceCollection AddSwagger(this IServiceCollection services)
    {
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo { Title = "Appointment API", Version = "v1" });

            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme 
            { 
                In = ParameterLocation.Header,
                Description = "Please enter a valid token",
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                BearerFormat = "JWT",
                Scheme = "Bearer"
            });

            c.CustomSchemaIds(t => t.FullName?.Replace("+", "."));

            c.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    []
                }
            });
        });
        return services;
    }

    #endregion

    public static IApplicationBuilder UseSwaggerAppointmentApi(this IApplicationBuilder app)
    {
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "Appointment API V1");
            // Dejamos Swagger en /swagger para que la raíz de la API
            // siga disponible para endpoints funcionales o health checks.
            c.RoutePrefix = "swagger";
        });

        return app;
    }

    public static bool IsSwaggerPublicEnabled(this IConfiguration configuration)
    {
        return bool.TryParse(configuration["Swagger:PublicEnabled"], out bool enabled) && enabled;
    }
}

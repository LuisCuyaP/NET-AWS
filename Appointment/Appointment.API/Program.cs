using Appointment.Api;
using Appointment.Infrastructure;
using Appointment.Api.Middlewares;
using Appointment.Api.Extensions;
using Appointment.Application;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

// Punto de entrada de la API principal.
// Aquí dejamos preparada la app para dos escenarios:
// 1. ejecución local desde Visual Studio / dotnet run
// 2. ejecución en contenedor detrás de un ALB en ECS Fargate
builder.ConfigureAppointmentApi();

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddProblemDetails();

// La solución sigue el estilo actual del workspace:
// - Presentation: minimal endpoints
// - Infrastructure: EF Core + AWS SNS/SQS + worker
// - Application: MediatR/CQRS
builder.Services
    .AddPresentation()
    .AddInfrastructure(builder.Configuration, builder.Environment)
    .AddApplication()
    .AddEndpoints(AssemblyReference.Assembly);
    
var app = builder.Build();

app.Use(async (context, next) =>
{
    context.Response.Headers.Append("Referrer-Policy", "same-origin");
    context.Response.Headers.Append("Strict-Transport-Security", "max-age=31536000");
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("X-XSS-Protection", "1");

    await next(context);
});

// Swagger se expone siempre en desarrollo para facilitar iteración local.
// En otros entornos queda bajo control explícito de configuración para no
// publicar documentación sensible por accidente.
if (app.Environment.IsDevelopment() || builder.Configuration.IsSwaggerPublicEnabled())
{
    app.UseSwaggerAppointmentApi();
}

// ECS/ALB termina TLS antes de llegar al contenedor.
// Este middleware toma X-Forwarded-For y X-Forwarded-Proto para que
// ASP.NET Core entienda el esquema/protocolo real de la petición.
app.UseForwardedHeaders();

// Mantenemos redirección HTTPS porque en AWS el cliente llegará por HTTPS
// al ALB y el header reenviado evitará redirecciones incorrectas.
app.UseHttpsRedirection();
app.UseCors("AllowSpecificOrigins");
//app.UseAuthorization();
app.UseMiddleware<ExceptionLoggingMiddleware>();

// Registra todos los endpoints minimal API descubiertos por reflexión,
// incluyendo endpoints funcionales como /appointments y el health check.
app.MapEndpoints();

// Para la Fase 3 en AWS podemos arrancar contra una base SQL Server vacía.
// Si la configuración lo habilita, EF crea la base y el esquema al iniciar
// el proceso para dejar la API lista para pruebas end-to-end.
app = await app.UseEnsureCreatedDatabaseAsync();

app.Run();

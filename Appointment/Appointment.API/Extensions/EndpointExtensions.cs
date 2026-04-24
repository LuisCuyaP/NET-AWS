using System.Reflection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Appointment.Api.Extensions;

public static class EndpointExtensions
{
    public static IServiceCollection AddEndpoints(this IServiceCollection services, params Assembly[] assemblies)
    {
        ServiceDescriptor[] serviceDescriptors = [.. assemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => typeof(IEndpoint).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
            .Select(t => ServiceDescriptor.Transient(typeof(IEndpoint), t))];

        services.TryAddEnumerable(serviceDescriptors);
        return services;
    }
    
    public static IApplicationBuilder MapEndpoints(this WebApplication app, RouteGroupBuilder? routeGroupBuilder = null)
    {
        IEnumerable<IEndpoint> endpoints = app.Services.GetRequiredService<IEnumerable<IEndpoint>>();
        IEndpointRouteBuilder builder = routeGroupBuilder is null ? app : routeGroupBuilder;
        foreach (IEndpoint endpoint in endpoints) { endpoint.MapEndpoint(builder); }
        return app;
    }
}
namespace Appointment.Api.Middlewares;

public class ExceptionLoggingMiddleware
{
    private readonly RequestDelegate _next;

    public ExceptionLoggingMiddleware(RequestDelegate next)
    {
        _next = next;
    }
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception)
        {
            // _telemetryClient.TrackEvent("[APPLICATION]", new Dictionary<string, string>
            // {
            //     { "Path", context.Request.Path },
            //     { "Method", context.Request.Method },
            //     { "User", context.User.Identity?.Name ?? "Anonymous" },
            //     { "ExceptionMessage", ex.Message },
            //     { "InnerException", ex.InnerException?.Message ?? string.Empty }
            // });
            throw;
        }
    }
}
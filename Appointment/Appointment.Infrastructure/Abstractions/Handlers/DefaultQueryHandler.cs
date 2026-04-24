using Microsoft.AspNetCore.WebUtilities;

namespace Appointment.Infrastructure.Abstractions.Handlers;

internal sealed class DefaultQueryHandler(IDictionary<string, string> defaults) : DelegatingHandler
{
    private readonly IDictionary<string, string> _defaults = defaults;
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.RequestUri is not null && _defaults is not null && _defaults.Count > 0)
        {
            var newUri = new Uri(QueryHelpers.AddQueryString(request.RequestUri.ToString(), _defaults!));
            request.RequestUri = newUri;
        }
        
        return base.SendAsync(request, cancellationToken);
    }
}

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Appointment.Application.Abstractions.Authentication;

namespace Appointment.Infrastructure.Database
{
    internal sealed class AuditoriaInterceptor(IUserContext userContext)
        : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (eventData.Context is not null)
                LogCambios(eventData.Context);

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        private void LogCambios(DbContext context)
        {
            EntityState[] tiposTransacciones = { EntityState.Added, EntityState.Modified, EntityState.Deleted };

            foreach (var entry in context.ChangeTracker.Entries())
            {
                if (!tiposTransacciones.Contains(entry.State))
                    continue;

                var properties = new Dictionary<string, string>
                {
                    ["UserId"] = userContext.RegistrationId ?? string.Empty,
                    ["Table"]  = entry.Entity.GetType().Name
                };

                var action = entry.State switch
                {
                    EntityState.Added   => "INSERT",
                    EntityState.Modified => "UPDATE",
                    EntityState.Deleted => "DELETE",
                    _ => string.Empty
                };

                properties.Add("Action", action);

                //telemetryClient.TrackEvent("[AUDITORIA]", properties);
            }
        }
    }
}
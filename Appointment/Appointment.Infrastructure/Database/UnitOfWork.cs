using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Appointment.Application.Abstractions.Authentication;
using Appointment.CrossCutting;
using Appointment.Domain.Database;
using System.Data;

namespace Appointment.Infrastructure.Database;

internal sealed class UnitOfWork(ApplicationDbContext context,
    IUserContext userContext) : IUnitOfWork
{
    public int SaveChanges() => context.SaveChanges();

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        DateTime now = DateTime.Now;
        var entries = context.ChangeTracker.Entries().Where(x => x.Entity is AuditableEntity);
        foreach (var entry in entries)
        {
            if (entry.Entity is AuditableEntity auditableEntity)
            {
                switch (entry.State)
                {
                    case EntityState.Added:
                        auditableEntity.AddCreateInfo(userContext.RegistrationId ?? string.Empty, now);
                        break;

                    case EntityState.Modified:
                        auditableEntity.AddModifyInfo(userContext.RegistrationId ?? string.Empty, now);
                        break;
                }
            }
        }

        var result = await context.SaveChangesAsync(cancellationToken);
        return result;
    }

    public void Dispose()
    {
        context.Dispose();
    }

    public IDbTransaction BeginTransaction()
    {
        var dbConnection = context.Database.GetDbConnection();
        if (dbConnection.State == ConnectionState.Closed)
        {
            dbConnection.Open();
        }
        return dbConnection.BeginTransaction();
    }

    public async Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        return await context.Database.BeginTransactionAsync(cancellationToken);
    }

}
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;

namespace Appointment.Domain.Database;

public interface IUnitOfWork : IDisposable
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    int SaveChanges();
    IDbTransaction BeginTransaction();
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
}

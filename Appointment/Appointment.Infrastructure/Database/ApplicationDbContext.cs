using Microsoft.EntityFrameworkCore;
using AppointmentEntity = Appointment.Domain.AppointmentsAggregates.Appointment;

namespace Appointment.Infrastructure.Database;

internal sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<AppointmentEntity> Appointments { get; set; } = default!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
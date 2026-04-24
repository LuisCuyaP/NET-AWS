using AppointmentEntity = Appointment.Domain.AppointmentsAggregates.Appointment;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Appointment.Infrastructure.AppointmentsAggregates;

internal sealed class AppointmentConfiguration : IEntityTypeConfiguration<AppointmentEntity>
{
    public void Configure(EntityTypeBuilder<AppointmentEntity> builder)
    {
        builder.ToTable("Appointments");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.InsuredId)
            .HasMaxLength(5)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(x => x.ScheduleId)
            .IsRequired();

        builder.Property(x => x.CountryISO)
            .HasMaxLength(2)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasMaxLength(20)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(x => x.CreatedBy);

        builder.Property(x => x.ModifiedBy);

        builder.Property(x => x.CreatedDate)
            .HasColumnType("datetime2");

        builder.Property(x => x.ModifiedDate)
            .HasColumnType("datetime2");

        builder.HasIndex(x => x.InsuredId);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.CreatedDate);
    }
}

using System.Globalization;
using Amazon.DynamoDBv2.Model;
using Appointment.Pe.Domain.AppointmentsAggregates;

namespace Appointment.Pe.Infrastructure.AppointmentsAggregates;

public sealed class AppointmentPeDynamoEntity
{
    public string AppointmentId { get; set; } = default!;
    public string InsuredId { get; set; } = default!;
    public int ScheduleId { get; set; }
    public string CountryISO { get; set; } = default!;
    public string Status { get; set; } = default!;
    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTime? CreatedDate { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public DateTime ProcessedAt { get; set; }

    public static AppointmentPeDynamoEntity FromDomain(AppointmentPeRecord record)
        => new()
        {
            AppointmentId = record.AppointmentId.ToString("D"),
            InsuredId = record.InsuredId,
            ScheduleId = record.ScheduleId,
            CountryISO = record.CountryISO,
            Status = record.Status,
            CreatedBy = record.CreatedBy,
            ModifiedBy = record.ModifiedBy,
            CreatedDate = record.CreatedDate,
            ModifiedDate = record.ModifiedDate,
            ProcessedAt = record.ProcessedAt
        };

    public static AppointmentPeDynamoEntity FromItem(Dictionary<string, AttributeValue> item)
    {
        item.TryGetValue("createdBy", out var createdBy);
        item.TryGetValue("modifiedBy", out var modifiedBy);
        item.TryGetValue("createdDate", out var createdDate);
        item.TryGetValue("modifiedDate", out var modifiedDate);

        if (createdDate is null && item.TryGetValue("createdAt", out var createdAt))
        {
            createdDate = createdAt;
        }

        return new()
        {
            AppointmentId = item["AppointmentId"].S,
            InsuredId = item["insuredId"].S,
            ScheduleId = int.Parse(item["scheduleId"].N, CultureInfo.InvariantCulture),
            CountryISO = item["countryISO"].S,
            Status = item["status"].S,
            CreatedBy = createdBy?.S,
            ModifiedBy = modifiedBy?.S,
            CreatedDate = createdDate?.S is { Length: > 0 }
                ? DateTime.Parse(createdDate.S, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
                : null,
            ModifiedDate = modifiedDate?.S is { Length: > 0 }
                ? DateTime.Parse(modifiedDate.S, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
                : null,
            ProcessedAt = DateTime.Parse(item["processedAt"].S, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
        };
    }

    public Dictionary<string, AttributeValue> ToItem()
    {
        var item = new Dictionary<string, AttributeValue>
        {
            ["AppointmentId"] = new AttributeValue { S = AppointmentId },
            ["insuredId"] = new AttributeValue { S = InsuredId },
            ["scheduleId"] = new AttributeValue { N = ScheduleId.ToString(CultureInfo.InvariantCulture) },
            ["countryISO"] = new AttributeValue { S = CountryISO },
            ["status"] = new AttributeValue { S = Status },
            ["processedAt"] = new AttributeValue { S = ProcessedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture) }
        };

        if (!string.IsNullOrWhiteSpace(CreatedBy))
        {
            item["createdBy"] = new AttributeValue { S = CreatedBy };
        }

        if (!string.IsNullOrWhiteSpace(ModifiedBy))
        {
            item["modifiedBy"] = new AttributeValue { S = ModifiedBy };
        }

        if (CreatedDate is not null)
        {
            item["createdDate"] = new AttributeValue { S = CreatedDate.Value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture) };
            item["createdAt"] = new AttributeValue { S = CreatedDate.Value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture) };
        }

        if (ModifiedDate is not null)
        {
            item["modifiedDate"] = new AttributeValue { S = ModifiedDate.Value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture) };
        }

        return item;
    }

    public AppointmentPeRecord? ToDomainOrNull()
    {
        if (!Guid.TryParse(AppointmentId, out var appointmentId))
        {
            return null;
        }

        var result = AppointmentPeRecord.Restore(
            appointmentId,
            InsuredId,
            ScheduleId,
            CountryISO,
            Status,
            CreatedDate ?? default,
            ProcessedAt);

        return result.IsSuccess ? result.Value : null;
    }
}
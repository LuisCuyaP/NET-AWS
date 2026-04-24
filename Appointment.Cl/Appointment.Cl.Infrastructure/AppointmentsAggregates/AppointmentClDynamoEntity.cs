using System.Globalization;
using Amazon.DynamoDBv2.Model;
using Appointment.Cl.Domain.AppointmentsAggregates;

namespace Appointment.Cl.Infrastructure.AppointmentsAggregates;

public sealed class AppointmentClDynamoEntity
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

    public static AppointmentClDynamoEntity FromDomain(AppointmentClRecord record)
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

    public static AppointmentClDynamoEntity FromItem(Dictionary<string, AttributeValue> item)
        => new()
        {
            AppointmentId = item["AppointmentId"].S,
            InsuredId = item["insuredId"].S,
            ScheduleId = int.Parse(item["scheduleId"].N, CultureInfo.InvariantCulture),
            CountryISO = item["countryISO"].S,
            Status = item["status"].S,
            CreatedBy = item.TryGetValue("createdBy", out var createdBy) ? createdBy.S : null,
            ModifiedBy = item.TryGetValue("modifiedBy", out var modifiedBy) ? modifiedBy.S : null,
            CreatedDate = TryGetUtcDate(item, "createdDate") ?? TryGetUtcDate(item, "createdAt"),
            ModifiedDate = TryGetUtcDate(item, "modifiedDate") ?? TryGetUtcDate(item, "updatedAt"),
            ProcessedAt = DateTime.Parse(item["processedAt"].S, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
        };

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
        }

        if (ModifiedDate is not null)
        {
            item["modifiedDate"] = new AttributeValue { S = ModifiedDate.Value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture) };
        }

        return item;
    }

    public AppointmentClRecord? ToDomainOrNull()
    {
        if (!Guid.TryParse(AppointmentId, out var appointmentId))
        {
            return null;
        }

        if (CreatedDate is null)
        {
            return null;
        }

        var result = AppointmentClRecord.Restore(
            appointmentId,
            InsuredId,
            ScheduleId,
            CountryISO,
            Status,
            CreatedDate.Value,
            ProcessedAt);

        if (result.IsFailure)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(CreatedBy) || result.Value.CreatedDate is not null)
        {
            result.Value.AddCreateInfo(CreatedBy, result.Value.CreatedDate);
        }

        if (!string.IsNullOrWhiteSpace(ModifiedBy) || ModifiedDate is not null)
        {
            result.Value.AddModifyInfo(ModifiedBy, ModifiedDate);
        }

        return result.Value;
    }

    private static DateTime? TryGetUtcDate(Dictionary<string, AttributeValue> item, string key)
    {
        if (!item.TryGetValue(key, out var attribute) || string.IsNullOrWhiteSpace(attribute.S))
        {
            return null;
        }

        return DateTime.Parse(attribute.S, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
    }
}
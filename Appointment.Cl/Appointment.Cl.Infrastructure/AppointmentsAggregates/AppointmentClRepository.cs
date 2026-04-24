using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Appointment.Cl.Domain.AppointmentsAggregates;

namespace Appointment.Cl.Infrastructure.AppointmentsAggregates;

internal sealed class AppointmentClRepository(IAmazonDynamoDB dynamoDb, DynamoDbTableSettings tableSettings) : IAppointmentClRepository
{
    public async Task<AppointmentClRecord?> GetByAppointmentIdAsync(Guid appointmentId, CancellationToken cancellationToken = default)
    {
        var request = new GetItemRequest
        {
            TableName = tableSettings.ClAppointmentsTableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["AppointmentId"] = new AttributeValue { S = appointmentId.ToString("D") }
            },
            ConsistentRead = true
        };

        var response = await dynamoDb.GetItemAsync(request, cancellationToken);

        if (response.Item is null || response.Item.Count == 0)
        {
            return null;
        }

        var entity = AppointmentClDynamoEntity.FromItem(response.Item);
        return entity.ToDomainOrNull();
    }

    public async Task UpsertAsync(AppointmentClRecord record, CancellationToken cancellationToken = default)
    {
        var nowUtc = DateTime.UtcNow;

        var existsRequest = new GetItemRequest
        {
            TableName = tableSettings.ClAppointmentsTableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["AppointmentId"] = new AttributeValue { S = record.AppointmentId.ToString("D") }
            },
            ProjectionExpression = "AppointmentId",
            ConsistentRead = false
        };

        var existsResponse = await dynamoDb.GetItemAsync(existsRequest, cancellationToken);
        var exists = existsResponse.Item is not null && existsResponse.Item.Count > 0;

        if (!exists)
        {
            record.AddCreateInfo(user: record.CreatedBy ?? "appointment-cl", date: record.CreatedDate ?? nowUtc);
        }
        else
        {
            record.AddModifyInfo(user: record.ModifiedBy ?? "appointment-cl", date: nowUtc);
        }

        var entity = AppointmentClDynamoEntity.FromDomain(record);

        var request = new PutItemRequest
        {
            TableName = tableSettings.ClAppointmentsTableName,
            Item = entity.ToItem()
        };

        await dynamoDb.PutItemAsync(request, cancellationToken);
    }
}

internal sealed class DynamoDbTableSettings
{
    public string ClAppointmentsTableName { get; }

    public DynamoDbTableSettings(string clAppointmentsTableName)
    {
        ClAppointmentsTableName = string.IsNullOrWhiteSpace(clAppointmentsTableName)
            ? throw new ArgumentException("AWS:ClAppointmentsTableName is required.", nameof(clAppointmentsTableName))
            : clAppointmentsTableName.Trim();
    }
}
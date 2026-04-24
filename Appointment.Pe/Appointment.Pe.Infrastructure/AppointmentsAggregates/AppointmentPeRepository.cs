using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Appointment.Pe.CrossCutting;
using Appointment.Pe.Domain.AppointmentsAggregates;

namespace Appointment.Pe.Infrastructure.AppointmentsAggregates;

internal sealed class AppointmentPeRepository(IAmazonDynamoDB dynamoDb, DynamoDbTableSettings tableSettings) : IAppointmentPeRepository
{
    public async Task<AppointmentPeRecord?> GetByAppointmentIdAsync(Guid appointmentId, CancellationToken cancellationToken = default)
    {
        var request = new GetItemRequest
        {
            TableName = tableSettings.PeAppointmentsTableName,
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

        var entity = AppointmentPeDynamoEntity.FromItem(response.Item);
        return entity.ToDomainOrNull();
    }

    public async Task UpsertAsync(AppointmentPeRecord record, CancellationToken cancellationToken = default)
    {
        var nowUtc = DateTime.UtcNow;

        if (record is AuditableEntity auditable)
        {
            if (auditable.CreatedDate is null)
            {
                auditable.AddCreateInfo(user: null, date: nowUtc);
            }
            else
            {
                auditable.AddModifyInfo(user: null, date: nowUtc);
            }
        }

        var entity = AppointmentPeDynamoEntity.FromDomain(record);

        var request = new PutItemRequest
        {
            TableName = tableSettings.PeAppointmentsTableName,
            Item = entity.ToItem()
        };

        await dynamoDb.PutItemAsync(request, cancellationToken);
    }
}

internal sealed class DynamoDbTableSettings
{
    public string PeAppointmentsTableName { get; }

    public DynamoDbTableSettings(string peAppointmentsTableName)
    {
        PeAppointmentsTableName = string.IsNullOrWhiteSpace(peAppointmentsTableName)
            ? throw new ArgumentException("AWS:PeAppointmentsTableName is required.", nameof(peAppointmentsTableName))
            : peAppointmentsTableName.Trim();
    }
}
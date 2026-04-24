namespace Appointment.Application.Abstractions.AI;

public interface IAzureAIPromptExecutor
{
    Task<string> ExecutePromptAsync(string systemPromptContent, string userPromptContent, CancellationToken cancellationToken = default);
}

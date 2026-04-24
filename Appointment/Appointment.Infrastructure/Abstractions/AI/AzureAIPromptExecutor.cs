using Azure.AI.OpenAI;
using Azure.Identity;
using Appointment.Application.Abstractions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

namespace Appointment.Infrastructure.Abstractions.AI
{
    internal sealed class AzureAIPromptExecutor(IConfiguration configuration) : IAzureAIPromptExecutor
    {
        public async Task<string> ExecutePromptAsync(
            string systemPromptContent,
            string userPromptContent,
            CancellationToken cancellationToken = default)
        {
            // Credenciales AAD (app registration)
            var credential = new ClientSecretCredential(
                configuration["AzureAD:TenantId"],
                configuration["AzureAD:ClientId"],
                configuration["AzureAD:ClientSecret"]);

            // Cliente Azure OpenAI (SDK v2.x)
            var azClient = new AzureOpenAIClient(new Uri(configuration["OpenAI:URL"]!), credential);
            ChatClient chatClient = azClient.GetChatClient(configuration["OpenAI:DeploymentChat"]!);

            // Mensajes
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(systemPromptContent),
                new UserChatMessage(userPromptContent)
            };

            // Opciones de finalización
            var options = new ChatCompletionOptions
            {
                MaxOutputTokenCount = 32680,
                Temperature = 1.0f,
                TopP = 1,
                FrequencyPenalty = 0,
                PresencePenalty = 0
            };

            int intento = 1;
            do
            {
                ChatCompletion completion = await chatClient.CompleteChatAsync(messages, options, cancellationToken);

                if (completion is not null &&
                    completion.FinishReason != ChatFinishReason.ContentFilter)
                {
                    string texto = string.Empty;
                    foreach (var item in completion.Content)
                        texto += item.Text.Trim();

                    return texto;
                }

                intento++;
            }
            while (intento <= 5);

            return string.Empty;
        }
    }
}
using System.Collections.Concurrent;
using System.Text.Json;
using Azure.AI.Projects;
using Azure.AI.Extensions.OpenAI;
using Azure.Identity;
using OpenAI;

#pragma warning disable OPENAI001

namespace AGNT.WebappOrchestrator;

public sealed class FoundryAgentClient(IConfiguration config, ILogger<FoundryAgentClient> logger)
{
    private readonly bool _useMock = config.GetValue<bool>("AzureAIFoundry:UseMock", false)
                                     || string.IsNullOrWhiteSpace(config["AzureAIFoundry:Endpoint"]);

    private readonly AIProjectClient? _client =
        (!config.GetValue<bool>("AzureAIFoundry:UseMock", false) && !string.IsNullOrWhiteSpace(config["AzureAIFoundry:Endpoint"]))
            ? new AIProjectClient(new Uri(config["AzureAIFoundry:Endpoint"]!), new AzureCliCredential())
            : null;

    private readonly ConcurrentDictionary<string, string> _conversationThreads = new();

    public async Task<string> RunAgentAsync(string agentName, string message, string? conversationKey = null, CancellationToken ct = default)
    {
        if (_useMock || _client == null)
        {
            logger.LogInformation("[Foundry Mock] Simulando respuesta de agente '{Agent}' para mensaje: {Message}", agentName, message);
            return GenerateMockResponse(agentName);
        }

        try
        {
            // Recupera el cliente del agente de Foundry y prepara la conversación
            logger.LogInformation("[Foundry Cloud] Invocando agente '{Agent}' en Azure AI Foundry...", agentName);
            var agentClient = _client.ProjectOpenAIClient.GetProjectResponsesClientForAgentEndpoint(agentName);

            // Bloque de Gestión de memoria multiturno
            string? azureConvId = null;
            if (!string.IsNullOrEmpty(conversationKey))
            {
                if (!_conversationThreads.TryGetValue(conversationKey, out azureConvId))
                {
                    var conversationClient = new ProjectConversationsClient(
                        agentClient.Pipeline,
                        new OpenAIClientOptions { Endpoint = agentClient.Endpoint });

                    var newConv = await Task.Run(() => conversationClient.CreateProjectConversation(), ct);
                    azureConvId = newConv.Value.Id;
                    _conversationThreads[conversationKey] = azureConvId;
                    logger.LogInformation("[Foundry Cloud] Nueva conversación creada en Azure: {ConvId}", azureConvId);
                }
            }

            // Bloque de Invocación del agente y manejo de mensaje
            var responseClient = string.IsNullOrEmpty(azureConvId)
                ? agentClient
                : _client.ProjectOpenAIClient.GetProjectResponsesClientForAgentEndpoint(agentName, defaultConversationId: azureConvId);

            var response = await Task.Run(() => responseClient.CreateResponse(message), ct);
            var rawOutput = response.Value.GetOutputText();

            return FormatAgentOutput(rawOutput);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[Foundry Error] Falla al invocar agente '{Agent}'. Detalle: {Msg}", agentName, ex.Message);
            return $"[Azure AI Foundry Error]: No se pudo procesar con '{agentName}': {ex.Message}";
        }
    }

    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    private static string FormatAgentOutput(string? rawOutput)
    {
        if (string.IsNullOrWhiteSpace(rawOutput))
            return Constants.Messages.NoAgentResponse;

        var trimmed = rawOutput.Trim();

        // Validación previa: si no tiene estructura JSON con campo 'resultado', es texto conversacional
        if (!trimmed.StartsWith('{') || !trimmed.EndsWith('}') || !trimmed.Contains("resultado", StringComparison.OrdinalIgnoreCase))
        {
            return rawOutput;
        }

        try
        {
            var evaluation = JsonSerializer.Deserialize<CreditEvaluationResult>(trimmed, _jsonOptions);
            return !string.IsNullOrWhiteSpace(evaluation?.Resultado)
                ? evaluation.ToMarkdown()
                : rawOutput;
        }
        catch (JsonException)
        {
            // Si el JSON viene malformado o trunco, se retorna el texto crudo sin romper el flujo
            return rawOutput;
        }
    }

    private static string GenerateMockResponse(string agentName) =>
        agentName.Contains("creditici", StringComparison.OrdinalIgnoreCase)
            ? Constants.Mocks.CreditApproval
            : Constants.Mocks.GeneralInquiry;
}

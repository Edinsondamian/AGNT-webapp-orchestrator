using System.Collections.Concurrent;
using System.Globalization;
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
            logger.LogInformation("[Foundry Cloud] Invocando agente '{Agent}' en Azure AI Foundry...", agentName);
            var agentClient = _client.ProjectOpenAIClient.GetProjectResponsesClientForAgentEndpoint(agentName);

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

    private static string FormatAgentOutput(string? rawOutput)
    {
        if (string.IsNullOrWhiteSpace(rawOutput))
            return Constants.Messages.NoAgentResponse;

        try
        {
            using var doc = JsonDocument.Parse(rawOutput);
            var root = doc.RootElement;
            if (root.TryGetProperty("resultado", out var resElem))
            {
                var resultado = resElem.GetString() ?? "EVALUADO";
                var icon = resultado.Equals("APROBADO", StringComparison.OrdinalIgnoreCase) ? "✅" : "⚠️";
                var score = root.TryGetProperty("scoreCrediticio", out var s) ? s.ToString() : "-";
                var riesgo = root.TryGetProperty("nivelRiesgo", out var r) ? r.GetString() : "-";
                var monto = root.TryGetProperty("montoAprobado", out var m) ? m.GetDecimal().ToString("N2", CultureInfo.InvariantCulture) : "0.00";
                var cuota = root.TryGetProperty("cuotaEstimada", out var c) ? c.GetDecimal().ToString("N2", CultureInfo.InvariantCulture) : "0.00";
                var valId = root.TryGetProperty("idValidacion", out var v) ? v.GetString() : "-";

                string observacion = "";
                if (root.TryGetProperty("observaciones", out var obsArray) && obsArray.GetArrayLength() > 0)
                {
                    observacion = $"\n• **Observación**: {obsArray[0].GetString()}";
                }

                return string.Format(
                    Constants.Templates.CreditEvaluation,
                    icon, resultado, score, riesgo, monto, cuota, valId, observacion);
            }
        }
        catch
        {
        }

        return rawOutput;
    }

    private static string GenerateMockResponse(string agentName) =>
        agentName.Contains("creditici", StringComparison.OrdinalIgnoreCase)
            ? Constants.Mocks.CreditApproval
            : Constants.Mocks.GeneralInquiry;
}

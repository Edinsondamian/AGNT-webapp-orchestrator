using System.Globalization;
using System.Text.Json;
using Azure.AI.Extensions.OpenAI;
using Azure.AI.Projects;
using Azure.Identity;

#pragma warning disable OPENAI001

namespace AGNT.WebappOrchestrator;

public sealed class FoundryAgentClient
{
    private readonly IConfiguration _config;
    private readonly ILogger<FoundryAgentClient> _logger;
    private readonly bool _useMock;
    private readonly string _endpoint;
    private readonly Lazy<AIProjectClient> _lazyClient;

    public FoundryAgentClient(IConfiguration config, ILogger<FoundryAgentClient> logger)
    {
        _config = config;
        _logger = logger;
        _endpoint = _config["AzureAIFoundry:Endpoint"] ?? string.Empty;
        _useMock = _config.GetValue<bool>("AzureAIFoundry:UseMock", true) || string.IsNullOrWhiteSpace(_endpoint);

        _lazyClient = new Lazy<AIProjectClient>(() =>
            new AIProjectClient(new Uri(_endpoint), new DefaultAzureCredential()));
    }

    public async Task<string> RunAgentAsync(string agentName, string message, CancellationToken ct = default)
    {
        if (_useMock)
        {
            _logger.LogInformation("[Foundry Mock] Simulando respuesta de agente '{Agent}' para mensaje: {Message}", agentName, message);
            return GenerateMockResponse(agentName, message);
        }

        try
        {
            _logger.LogInformation("[Foundry Cloud] Invocando agente '{Agent}' en Azure AI Foundry...", agentName);
            var projectClient = _lazyClient.Value;
            var responseClient = projectClient.ProjectOpenAIClient.GetProjectResponsesClientForAgentEndpoint(agentName);

            var response = await Task.Run(() => responseClient.CreateResponse(message), ct);
            var output = response.Value.GetOutputText();

            return output ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Foundry Error] Falla al invocar agente '{Agent}'. Activando fallback simulado.", agentName);
            return GenerateMockResponse(agentName, message);
        }
    }

    private static string GenerateMockResponse(string agentName, string message)
    {
        if (agentName.Contains("creditici", StringComparison.OrdinalIgnoreCase))
        {
            return "✅ **Evaluación Crediticia Aprobada (Azure AI Foundry)**\n\n" +
                   "• **Estado**: APROBADO\n" +
                   "• **Score crediticio**: 745 (Nivel de riesgo: BAJO)\n" +
                   "• **Monto aprobado**: S/ 10,000.00\n" +
                   "• **Cuota mensual estimada**: S/ 945.50 a 12 meses\n" +
                   "• **Observaciones**: Actividad económica favorable. Proceso 100% digital sin requerir documentos físicos ni papeleos.\n" +
                   "• **Canal**: Microsoft 365 Agents Playground";
        }

        return "[Agente de Consultas Generales]: ¡Hola! Te oriento con gusto: para solicitar tu crédito solo necesitas tu DNI, el monto requerido y tu actividad económica. No requerimos documentos físicos ni papeleos. ¿En qué más puedo ayudarte?";
    }
}

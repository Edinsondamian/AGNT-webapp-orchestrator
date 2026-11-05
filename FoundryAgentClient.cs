using System.Globalization;
using System.Text.Json;
using Azure.Core;
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
        _useMock = _config.GetValue<bool>("AzureAIFoundry:UseMock", false) || string.IsNullOrWhiteSpace(_endpoint);

        // Asegurar que Azure CLI esté en el PATH
        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        if (!path.Contains("Azure\\CLI2\\wbin", StringComparison.OrdinalIgnoreCase) &&
            Directory.Exists(@"C:\Program Files\Microsoft SDKs\Azure\CLI2\wbin"))
        {
            Environment.SetEnvironmentVariable("PATH", path + @";C:\Program Files\Microsoft SDKs\Azure\CLI2\wbin");
        }

        _lazyClient = new Lazy<AIProjectClient>(() =>
        {
            TokenCredential credential;
            try
            {
                // Autenticación directa y rápida con az login
                credential = new AzureCliCredential();
            }
            catch
            {
                credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
                {
                    ExcludeManagedIdentityCredential = true
                });
            }

            return new AIProjectClient(new Uri(_endpoint), credential);
        });
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
            var rawOutput = response.Value.GetOutputText();

            return FormatAgentOutput(agentName, rawOutput);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Foundry Error] Falla al invocar agente '{Agent}'. Detalle: {Msg}", agentName, ex.Message);
            return $"[Azure AI Foundry Error]: No se pudo procesar con '{agentName}': {ex.Message}";
        }
    }

    private static string FormatAgentOutput(string agentName, string? rawOutput)
    {
        if (string.IsNullOrWhiteSpace(rawOutput))
            return "No se obtuvo respuesta del agente.";

        // Si el agente crediticio devolvió JSON estructurado, formatearlo elegantemente para el chat
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

                return $"{icon} **Evaluación Crediticia — Azure AI Foundry**\n\n" +
                       $"• **Resultado**: {resultado}\n" +
                       $"• **Score crediticio**: {score} (Riesgo: {riesgo})\n" +
                       $"• **Monto aprobado**: S/ {monto} PEN\n" +
                       $"• **Cuota estimada**: S/ {cuota} / mes\n" +
                       $"• **Validación**: {valId}\n" +
                       $"• **Modalidad**: 100% digital sin requerir documentos físicos ni papeleos.";
            }
        }
        catch
        {
            // No es JSON, devolver el texto tal cual
        }

        return rawOutput;
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

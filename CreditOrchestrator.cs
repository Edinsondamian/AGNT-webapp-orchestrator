using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AGNT.WebappOrchestrator;

public sealed class CreditOrchestrator(
    FoundryAgentClient foundryClient,
    IConfiguration config,
    IHttpClientFactory httpClientFactory,
    ILogger<CreditOrchestrator> logger)
{
    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    // --- Prompt de Enrutamiento Inteligente (OpenAI Decisions API - Choice Pattern) ---
    public const string RoutingQuestionName = "target_agent";
    public const string RoutingInstructions =
        "Determina a qué agente especializado de Azure AI Foundry se debe derivar la consulta del usuario:";

    public static readonly List<DecisionChoice> RoutingChoices =
    [
        new("credit", "Solicitudes de crédito, montos, cuotas, evaluación crediticia, DNI, capacidad de pago o temas financieros."),
        new("documental", "Consultas puramente informativas sobre requisitos documentales, qué papeles o documentos se solicitan, políticas de documentación o condiciones generales.")
    ];

    public async Task<string> ProcessMessageAsync(
        string message,
        string? conversationId = null,
        CancellationToken ct = default)
    {
        var cleanMessage = message?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(cleanMessage))
        {
            return Constants.Messages.EmptyMessagePrompt;
        }

        // 1. Decisión del Agente vía Prompt (Decisions API)
        var decision = await DecideAgentAsync(cleanMessage, ct);

        // 2. Mapeo de la elección al agente correspondiente de Azure AI Foundry
        var targetAgent = decision switch
        {
            "documental" => config["AzureAIFoundry:Agents:General"] ?? "Agente-evaluacion-documental",
            _ => config["AzureAIFoundry:Agents:Credit"] ?? "agente-evaluacion-crediticia"
        };

        logger.LogInformation("[Orquestador Decisions Prompt] Decisión tomada: '{Decision}' -> Derivando a '{Agent}' (Conv: {ConvId})",
            decision, targetAgent, conversationId);

        // 3. Invocación al agente especializado
        return await foundryClient.RunAgentAsync(targetAgent, cleanMessage, conversationId, ct);
    }

    private async Task<string> DecideAgentAsync(string input, CancellationToken ct)
    {
        var model = config["Decisions:Model"] ?? "gpt-6-luna";
        var endpoint = config["Decisions:Endpoint"] ?? "https://api.openai.com/v1/decisions";
        var apiKey = config["Decisions:ApiKey"] ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");

        // Construir la petición formal de Decisions API (POST /v1/decisions)
        var decisionRequest = new DecisionRequest(
            Model: model,
            Input: input,
            Questions:
            [
                new DecisionQuestion(
                    Type: "choice",
                    Name: RoutingQuestionName,
                    Instructions: RoutingInstructions,
                    Choices: RoutingChoices)
            ]);

        // Si hay API Key configurada para Decisions API, llamamos al endpoint en vivo
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            try
            {
                var client = httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

                var httpResp = await client.PostAsJsonAsync(endpoint, decisionRequest, ct);
                if (httpResp.IsSuccessStatusCode)
                {
                    var decisionResp = await httpResp.Content.ReadFromJsonAsync<DecisionResponse>(_jsonOptions, ct);
                    var choice = decisionResp?.Answers?.FirstOrDefault(a => a.Name == RoutingQuestionName)?.Choice;
                    if (!string.IsNullOrWhiteSpace(choice))
                    {
                        logger.LogInformation("[Decisions API Live] Respuesta: Choice='{Choice}'", choice);
                        return choice.ToLowerInvariant();
                    }
                }
                else
                {
                    logger.LogWarning("[Decisions API] Falló respuesta ({StatusCode}): {Reason}", httpResp.StatusCode, httpResp.ReasonPhrase);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "[Decisions API] Error al invocar Decisions API: {Msg}", ex.Message);
            }
        }

        // Evaluación basada en las opciones del Prompt de Enrutamiento (Fallback autónomo)
        return EvaluateDecisionPrompt(input);
    }

    private static string EvaluateDecisionPrompt(string input)
    {
        var lower = input.ToLowerInvariant();

        // Opción 'documental': Preguntas de requisitos, papeles o documentos sin DNI
        bool isDocumental = lower.Contains("requisito") || lower.Contains("condicion") || lower.Contains("condición")
                         || lower.Contains("documento") || lower.Contains("papel") || lower.Contains("tasa")
                         || lower.Contains("interes") || lower.Contains("interés") || lower.Contains("politica")
                         || lower.Contains("política") || lower.Contains("que piden") || lower.Contains("qué piden");

        bool hasDni = Regex.IsMatch(input, @"\b\d{8}\b");

        if (isDocumental && !hasDni)
        {
            return "documental";
        }

        // Opción 'credit': Solicitudes, evaluaciones, montos o conversación crediticia
        return "credit";
    }
}

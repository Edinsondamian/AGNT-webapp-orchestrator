using System.Text.RegularExpressions;

namespace AGNT.WebappOrchestrator;

public sealed class CreditOrchestrator
{
    private readonly FoundryAgentClient _foundryClient;
    private readonly IConfiguration _config;
    private readonly ILogger<CreditOrchestrator> _logger;

    public CreditOrchestrator(
        FoundryAgentClient foundryClient,
        IConfiguration config,
        ILogger<CreditOrchestrator> logger)
    {
        _foundryClient = foundryClient;
        _config = config;
        _logger = logger;
    }

    public async Task<ChatResponse> ProcessMessageAsync(
        string message,
        string? conversationId = null,
        CancellationToken ct = default)
    {
        conversationId ??= $"conv-{Guid.NewGuid():N}";
        var cleanMessage = message?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(cleanMessage))
        {
            return new ChatResponse(
                "Por favor, escribe tu consulta o indícame tu número de DNI para comenzar.",
                conversationId,
                "Orquestador");
        }

        var lower = cleanMessage.ToLowerInvariant();

        // 1. Detectar si es una consulta informativa (requisitos, condiciones, documentos)
        bool isInformational = lower.Contains("requisito") || lower.Contains("condicion") || lower.Contains("condición")
                               || lower.Contains("documento") || lower.Contains("papel") || lower.Contains("tasa")
                               || lower.Contains("interes") || lower.Contains("interés") || lower.Contains("como funciona")
                               || lower.Contains("cómo funciona") || lower.Contains("informacion") || lower.Contains("información");

        var dniMatch = Regex.Match(cleanMessage, @"\b(\d{8})\b");

        // Si es puramente informativa y no trae DNI, derivamos al agente documental
        if (isInformational && !dniMatch.Success)
        {
            var generalAgent = _config["AzureAIFoundry:Agents:General"] ?? "Agente-evaluacion-documental";
            _logger.LogInformation("[Orquestador] Consulta informativa. Derivando a '{Agent}'...", generalAgent);
            var reply = await _foundryClient.RunAgentAsync(generalAgent, cleanMessage, conversationId, ct);
            return new ChatResponse(reply, conversationId, generalAgent);
        }

        // 2. Derivación al Agente de Evaluación Crediticia en Azure AI Foundry
        // Se mantiene el hilo conversacional nativo (ProjectConversation) para que el agente
        // recuerde los turnos anteriores (DNI, montos, etc.) tal como en el portal de Azure.
        var creditAgent = _config["AzureAIFoundry:Agents:Credit"] ?? "agente-evaluacion-crediticia";
        _logger.LogInformation("[Orquestador] Derivando mensaje al agente '{Agent}' (Conv: {ConvId})...", creditAgent, conversationId);
        
        var result = await _foundryClient.RunAgentAsync(creditAgent, cleanMessage, conversationId, ct);

        return new ChatResponse(result, conversationId, creditAgent);
    }
}

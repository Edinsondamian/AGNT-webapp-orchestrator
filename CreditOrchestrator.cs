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

        // Derivación directa y desacoplada al Agente Cognitivo en Azure AI Foundry.
        // Se mantiene el hilo conversacional nativo (ProjectConversation) para que el agente
        // gestione naturalmente las intenciones, preguntas y evaluación con memoria de turnos.
        var targetAgent = _config["AzureAIFoundry:Agents:Credit"] ?? "agente-evaluacion-crediticia";
        _logger.LogInformation("[Orquestador] Canalizando mensaje directamente a '{Agent}' (Conv: {ConvId})...", targetAgent, conversationId);

        var result = await _foundryClient.RunAgentAsync(targetAgent, cleanMessage, conversationId, ct);

        return new ChatResponse(result, conversationId, targetAgent);
    }
}

namespace AGNT.WebappOrchestrator;

public sealed class CreditOrchestrator(
    FoundryAgentClient foundryClient,
    IConfiguration config,
    ILogger<CreditOrchestrator> logger)
{
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

        var targetAgent = config["AzureAIFoundry:Agents:Credit"] ?? "agente-evaluacion-crediticia";
        logger.LogInformation("[Orquestador] Canalizando mensaje directamente a '{Agent}' (Conv: {ConvId})...", targetAgent, conversationId);

        return await foundryClient.RunAgentAsync(targetAgent, cleanMessage, conversationId, ct);
    }
}

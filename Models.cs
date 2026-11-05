namespace AGNT.WebappOrchestrator;

public sealed record BotActivity(
    string? Type,
    string? Id,
    string? Text,
    BotAccount? From,
    BotAccount? Recipient,
    BotConversation? Conversation,
    string? ChannelId,
    string? ServiceUrl);

public sealed record BotAccount(
    string? Id,
    string? Name,
    string? Role = null,
    string? AadObjectId = null);

public sealed record BotConversation(
    string? Id,
    string? Name = null,
    string? ConversationType = null,
    string? TenantId = null);

public sealed record OutgoingActivity(
    string Type,
    string? Id,
    string? ReplyToId,
    DateTimeOffset Timestamp,
    string? ChannelId,
    BotAccount? From,
    BotAccount? Recipient,
    BotConversation? Conversation,
    string Text);

public sealed record BotResponse(
    string Type,
    string Text,
    BotConversation? Conversation = null);

public sealed record ChatRequest(
    string? Message,
    string? ConversationId = null);

public sealed record ChatResponse(
    string Response,
    string ConversationId,
    string Agent,
    bool Success = true);

public sealed class CreditInfo
{
    public string? Dni { get; set; }
    public decimal? Monto { get; set; }
    public string? ActividadEconomica { get; set; }
    public int PlazoMeses { get; set; } = 12;

    public bool HasRequiredData =>
        !string.IsNullOrWhiteSpace(Dni) &&
        Monto > 0 &&
        !string.IsNullOrWhiteSpace(ActividadEconomica);
}

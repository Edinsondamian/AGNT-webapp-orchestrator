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

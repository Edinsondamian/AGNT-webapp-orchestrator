using System.Globalization;

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

public sealed record CreditEvaluationResult(
    string? IdValidacion,
    string? Resultado,
    int? ScoreCrediticio,
    string? NivelRiesgo,
    decimal? MontoAprobado,
    decimal? CuotaEstimada,
    List<string>? Observaciones)
{
    public string ToMarkdown()
    {
        var icon = string.Equals(Resultado, "APROBADO", StringComparison.OrdinalIgnoreCase) ? "✅" : "⚠️";
        var obs = Observaciones is { Count: > 0 } ? $"\n• **Observación**: {Observaciones[0]}" : "";

        return string.Format(
            Constants.Templates.CreditEvaluation,
            icon,
            Resultado ?? "EVALUADO",
            ScoreCrediticio?.ToString() ?? "-",
            NivelRiesgo ?? "-",
            MontoAprobado?.ToString("N2", CultureInfo.InvariantCulture) ?? "0.00",
            CuotaEstimada?.ToString("N2", CultureInfo.InvariantCulture) ?? "0.00",
            IdValidacion ?? "-",
            obs);
    }
}

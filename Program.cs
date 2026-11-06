using AGNT.WebappOrchestrator;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient();
builder.Services.AddCors(opt => opt.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

builder.Services.AddSingleton<FoundryAgentClient>();
builder.Services.AddSingleton<CreditOrchestrator>();

var app = builder.Build();

app.UseCors();
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "AGNT Web App Orchestrator v1");
    c.RoutePrefix = string.Empty;
});

// Endpoint único para Microsoft 365 Agents Playground
app.MapPost("/api/messages", async (
    BotActivity activity,
    CreditOrchestrator orchestrator,
    IHttpClientFactory httpClientFactory,
    ILogger<Program> logger,
    CancellationToken ct) =>
{
    var convId = activity.Conversation?.Id;
    var botAccount = new BotAccount(activity.Recipient?.Id ?? "bot", activity.Recipient?.Name ?? "Test Bot", "bot");
    var userAccount = new BotAccount(activity.From?.Id ?? "user", activity.From?.Name ?? "Usuario", "user");

    // 1. Manejo de bienvenida
    if (string.Equals(activity.Type, "conversationUpdate", StringComparison.OrdinalIgnoreCase))
    {
        var welcomeActivity = new OutgoingActivity(
            Type: "message",
            Id: Guid.NewGuid().ToString(),
            ReplyToId: activity.Id,
            Timestamp: DateTimeOffset.UtcNow,
            ChannelId: activity.ChannelId ?? "msteams",
            From: botAccount,
            Recipient: userAccount,
            Conversation: activity.Conversation,
            Text: Constants.Messages.Welcome);

        await TrySendToConnectorAsync(httpClientFactory.CreateClient(), activity.ServiceUrl, convId, welcomeActivity, logger, ct);
        return Results.Ok(welcomeActivity);
    }

    // 2. Procesar mensaje del usuario
    var userText = activity.Text?.Trim() ?? string.Empty;
    var reply = await orchestrator.ProcessMessageAsync(userText, convId, ct);

    var responseActivity = new OutgoingActivity(
        Type: "message",
        Id: Guid.NewGuid().ToString(),
        ReplyToId: activity.Id,
        Timestamp: DateTimeOffset.UtcNow,
        ChannelId: activity.ChannelId ?? "msteams",
        From: botAccount,
        Recipient: userAccount,
        Conversation: activity.Conversation,
        Text: reply);

    // 3. Notificar al conector del playground para que pinte la burbuja en la ventana de chat
    await TrySendToConnectorAsync(httpClientFactory.CreateClient(), activity.ServiceUrl, convId, responseActivity, logger, ct);

    return Results.Ok(responseActivity);
});

static async Task TrySendToConnectorAsync(
    HttpClient client,
    string? serviceUrl,
    string? conversationId,
    OutgoingActivity activity,
    ILogger logger,
    CancellationToken ct)
{
    if (string.IsNullOrWhiteSpace(serviceUrl) || string.IsNullOrWhiteSpace(conversationId))
        return;

    var endpoint = $"{serviceUrl.TrimEnd('/')}/v3/conversations/{conversationId}/activities";
    try
    {
        var resp = await client.PostAsJsonAsync(endpoint, activity, ct);
        logger.LogInformation("Enviado al Playground Connector ({StatusCode}): {Url}", resp.StatusCode, endpoint);
    }
    catch (Exception ex)
    {
        logger.LogWarning("No se pudo enviar al Connector ({Url}): {Error}", endpoint, ex.Message);
    }
}

app.Run();

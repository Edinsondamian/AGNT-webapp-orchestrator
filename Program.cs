using AGNT.WebappOrchestrator;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Servicios esenciales y simples
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(opt => opt.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

builder.Services.AddSingleton<FoundryAgentClient>();
builder.Services.AddSingleton<CreditOrchestrator>();

var app = builder.Build();

app.UseCors();
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "AGNT Web App Orchestrator (Simplificado) v1");
    c.RoutePrefix = string.Empty;
});

// =========================================================================
// 1. Endpoint para Microsoft 365 Agents Playground (@microsoft/m365agentsplayground)
// =========================================================================
app.MapPost("/api/messages", async (
    [FromBody] BotActivity activity,
    CreditOrchestrator orchestrator,
    CancellationToken ct) =>
{
    // Bienvenida al unirse en Microsoft 365 Agents Playground
    if (string.Equals(activity.Type, "conversationUpdate", StringComparison.OrdinalIgnoreCase))
    {
        var welcome = "¡Hola! 👋 Bienvenido al Asistente de Evaluación Crediticia en Microsoft 365 Agents Playground. " +
                      "Para evaluar tu crédito 100% digital (sin requerir documentos físicos ni papeleos), " +
                      "por favor indícame tu número de DNI, el monto que deseas solicitar y tu actividad económica.";

        return Results.Ok(new BotResponse("message", welcome, activity.Conversation));
    }

    // Mensaje del usuario
    var userText = activity.Text?.Trim() ?? string.Empty;
    var convId = activity.Conversation?.Id;

    var result = await orchestrator.ProcessMessageAsync(userText, convId, ct);

    return Results.Ok(new BotResponse("message", result.Response, activity.Conversation));
})
.WithName("M365PlaygroundMessages")
.WithTags("Microsoft 365 Agents Playground")
.WithSummary("Endpoint para conectar con @microsoft/m365agentsplayground");

// =========================================================================
// 2. Endpoint REST estándar para pruebas directas vía Postman / curl
// =========================================================================
app.MapPost("/api/chat", async (
    [FromBody] ChatRequest request,
    CreditOrchestrator orchestrator,
    CancellationToken ct) =>
{
    var result = await orchestrator.ProcessMessageAsync(request.Message ?? string.Empty, request.ConversationId, ct);
    return Results.Ok(result);
})
.WithName("ChatApi")
.WithTags("Orchestrator")
.WithSummary("Endpoint REST para enviar consultas al orquestador");

// =========================================================================
// 3. Health Check
// =========================================================================
app.MapGet("/health", () => Results.Ok(new { status = "Healthy", timestamp = DateTime.UtcNow }));

app.Run();

using System.Globalization;
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
                "Por favor, escribe tu consulta o indícame tu número de DNI, monto y actividad económica.",
                conversationId,
                "Orquestador");
        }

        // 1. Detección de entidades conversacionales (DNI, Monto, Actividad Económica)
        var creditInfo = ExtractCreditEntities(cleanMessage);

        var lower = cleanMessage.ToLowerInvariant();
        bool hasCreditIntent = creditInfo.Dni != null 
                               || creditInfo.Monto != null
                               || lower.Contains("credito") || lower.Contains("crédito")
                               || lower.Contains("prestamo") || lower.Contains("préstamo")
                               || lower.Contains("evalua") || lower.Contains("financiar");

        // 2. Si no hay intención de crédito y es saludo o consulta general
        if (!hasCreditIntent)
        {
            var generalAgent = _config["AzureAIFoundry:Agents:General"] ?? "Agente-consultas-generales";
            var reply = await _foundryClient.RunAgentAsync(generalAgent, cleanMessage, ct);
            return new ChatResponse(reply, conversationId, generalAgent);
        }

        // 3. REGLA CLAVE: Solicitar obligatoriamente la actividad económica si no fue indicada
        if (string.IsNullOrWhiteSpace(creditInfo.ActividadEconomica))
        {
            var askActivity = "¡Hola! Para realizar tu evaluación crediticia sin requerir ningún documento físico ni papeleos, " +
                              "por favor indícame tu **actividad económica** o rubro de tu negocio (por ejemplo: bodega, transporte, comercio, restaurante, servicios, etc.).";

            return new ChatResponse(askActivity, conversationId, "Orquestador");
        }

        // 4. Si contamos con los datos (DNI, Monto y Actividad Económica), invocamos al Agente de Foundry
        var creditAgent = _config["AzureAIFoundry:Agents:Credit"] ?? "Agente-evaluacion-crediticia";
        
        var payloadForAgent = $"Solicitud de crédito para DNI: {creditInfo.Dni ?? "No especificado"}, " +
                              $"Monto: S/ {(creditInfo.Monto.HasValue ? creditInfo.Monto.Value.ToString("N2", CultureInfo.InvariantCulture) : "10,000.00")}, " +
                              $"Actividad económica: {creditInfo.ActividadEconomica}, " +
                              $"Plazo: {creditInfo.PlazoMeses} meses. Sin documentos requeridos.";

        _logger.LogInformation("[Orquestador] Derivando solicitud al agente '{Agent}'...", creditAgent);
        var evaluationResult = await _foundryClient.RunAgentAsync(creditAgent, payloadForAgent, ct);

        return new ChatResponse(evaluationResult, conversationId, creditAgent);
    }

    private static CreditInfo ExtractCreditEntities(string text)
    {
        var info = new CreditInfo();

        // Extraer DNI (8 dígitos)
        var dniMatch = Regex.Match(text, @"\b(\d{8})\b");
        if (dniMatch.Success)
            info.Dni = dniMatch.Groups[1].Value;

        // Extraer Monto
        var montoMatch = Regex.Match(text, @"(?:s\/?\.?\s*|\$)?(\d{1,3}(?:,\d{3})*(?:\.\d+)?|\d{3,7})", RegexOptions.IgnoreCase);
        if (montoMatch.Success && decimal.TryParse(montoMatch.Groups[1].Value.Replace(",", ""), NumberStyles.Any, CultureInfo.InvariantCulture, out var m))
        {
            if (m.ToString(CultureInfo.InvariantCulture) != info.Dni)
                info.Monto = m;
        }

        // Extraer Plazo
        var plazoMatch = Regex.Match(text, @"(\d{1,2})\s*meses", RegexOptions.IgnoreCase);
        if (plazoMatch.Success && int.TryParse(plazoMatch.Groups[1].Value, out var p))
            info.PlazoMeses = p;

        // Extraer Actividad Económica común
        string[] activities =
        [
            "bodega", "abarrote", "comercio", "transporte", "ferreteria", "ferretería",
            "restaurante", "comida", "tienda", "servicios", "consultoria", "consultoría",
            "taller", "mecanica", "mecánica", "farmacia", "panaderia", "panadería",
            "confeccion", "confección", "textil", "manufactura", "agricultura", "empleado",
            "independiente", "dependiente", "profesor", "abogado", "ingeniero", "ventas", "negocio"
        ];

        var lower = text.ToLowerInvariant();
        foreach (var act in activities)
        {
            if (lower.Contains(act))
            {
                info.ActividadEconomica = char.ToUpper(act[0]) + act[1..];
                break;
            }
        }

        return info;
    }
}

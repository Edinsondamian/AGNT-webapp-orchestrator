namespace AGNT.WebappOrchestrator;

public static class Constants
{
    public static class Messages
    {
        public const string EmptyMessagePrompt = "Por favor, escribe tu consulta o indícame tu número de DNI para comenzar.";

        public const string Welcome = "¡Hola! 👋 Bienvenido al Asistente de Evaluación Crediticia en Microsoft 365 Agents Playground.\n\n" +
                                      "Para evaluar tu crédito 100% digital (sin requerir documentos físicos ni papeleos), " +
                                      "por favor indícame tu **número de DNI**, el **monto** que deseas solicitar y tu **actividad económica**.";

        public const string NoAgentResponse = "No se obtuvo respuesta del agente.";
    }

    public static class Mocks
    {
        public const string CreditApproval = "✅ **Evaluación Crediticia Aprobada (Azure AI Foundry)**\n\n" +
                                             "• **Estado**: APROBADO\n" +
                                             "• **Score crediticio**: 745 (Nivel de riesgo: BAJO)\n" +
                                             "• **Monto aprobado**: S/ 10,000.00\n" +
                                             "• **Cuota mensual estimada**: S/ 945.50 a 12 meses\n" +
                                             "• **Observaciones**: Actividad económica favorable. Proceso 100% digital sin requerir documentos físicos ni papeleos.\n" +
                                             "• **Canal**: Microsoft 365 Agents Playground";

        public const string GeneralInquiry = "[Agente de Consultas Generales]: ¡Hola! Te oriento con gusto: para solicitar tu crédito solo necesitas tu DNI, el monto requerido y tu actividad económica. No requerimos documentos físicos ni papeleos. ¿En qué más puedo ayudarte?";
    }

    public static class Templates
    {
        public const string CreditEvaluation = "{0} **Evaluación Crediticia — Azure AI Foundry**\n\n" +
                                               "• **Resultado**: {1}\n" +
                                               "• **Score crediticio**: {2} (Riesgo: {3})\n" +
                                               "• **Monto aprobado**: S/ {4} PEN\n" +
                                               "• **Cuota estimada**: S/ {5} / mes\n" +
                                               "• **Validación**: {6}{7}\n" +
                                               "• **Modalidad**: 100% digital sin requerir documentos físicos ni papeleos.";
    }
}

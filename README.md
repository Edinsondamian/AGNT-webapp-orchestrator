# AGNT Web App Orchestrator (Versión Simplificada)

Proyecto **super sencillo y funcional** para la orquestación conversacional de evaluación crediticia con **Azure AI Foundry**, diseñado para probarse visualmente en el playground oficial de Microsoft **`@microsoft/m365agentsplayground`**.

---

## 🎯 ¿Cómo funciona? (Explicación para tu cliente)

1. **CERO Documentos**:
   - No se piden PDFs, imágenes ni trámites físicos.
   - Todo es 100% conversacional por el chat de Microsoft 365.

2. **Datos que solicita**:
   - **Número de DNI**: 8 dígitos.
   - **Monto**: en Soles (PEN).
   - **Actividad económica**: El orquestador **solicita obligatoriamente** la actividad económica (ej. bodega, transporte, restaurante, comercio, etc.) si el usuario no la escribió.
   - **Plazo**: en meses (12 meses por defecto).

3. **Conexión con Azure AI Foundry**:
   - Consultas generales o saludos ➔ `Agente-consultas-generales`.
   - Solicitudes de crédito con datos completos ➔ `agente-evaluacion-crediticia` en Azure AI Foundry para aprobación, score y cuota mensual.

---

## 📁 Estructura del Proyecto (1 Solo Proyecto, Cero Complicaciones)

```text
AGNT-webapp-orchestrator-simplificado/
├── Program.cs             # Endpoints /api/messages (M365 Playground) y /api/chat
├── CreditOrchestrator.cs  # Lógica sencilla: valida DNI, monto y pide actividad económica
├── FoundryAgentClient.cs  # Cliente simple para llamar a Azure AI Foundry (con modo simulación)
├── Models.cs              # Modelos simples de actividad de bot y crédito
├── appsettings.json       # Configuración de endpoint y agentes de Foundry
└── README.md
```

---

## 🚀 Cómo Ejecutar y Probar Visualmente

### 1. Iniciar la aplicación
Abre tu terminal en esta carpeta y ejecuta:
```bash
dotnet run
```
*Iniciará en `http://localhost:5000` (también puedes abrir Swagger en `http://localhost:5000` si deseas probarlo por navegador).*

### 2. Abrir Microsoft 365 Agents Playground
En otra terminal, ejecuta el playground oficial de Microsoft de npm:
```bash
npx @microsoft/m365agentsplayground -e http://localhost:5000/api/messages
```

Se abrirá automáticamente el emulador de chat en tu navegador.

---

## 💬 Flujo de Prueba en el Chat

1. **Saludo**: El asistente te dará la bienvenida pidiendo tu DNI, monto y actividad económica.
2. **Si no indicas tu actividad económica**:
   > Usuario: *"Hola, mi DNI es 12345678 y requiero 10000 soles"*
   > 
   > Asistente: *"¡Hola! Para realizar tu evaluación crediticia sin requerir ningún documento físico ni papeleos, por favor indícame tu **actividad económica** o rubro de tu negocio (por ejemplo: bodega, transporte, comercio, restaurante, servicios, etc.)."*
3. **Al indicar la actividad económica**:
   > Usuario: *"Tengo una bodega de abarrotes"*
   > 
   > Asistente: Retorna el resultado de Azure AI Foundry con la aprobación, score crediticio y cuota estimada.

---

## ⚙️ Configuración (`appsettings.json`)

```json
{
  "AzureAIFoundry": {
    "Endpoint": "https://<tu-recurso>.services.ai.azure.com/api/projects/<tu-proyecto>",
    "UseMock": false,
    "Agents": {
      "Credit": "agente-evaluacion-crediticia",
      "General": "Agente-consultas-generales"
    }
  }
}
```

> 💡 **Modo Simulación Local**: En `appsettings.Development.json` está configurado `"UseMock": true`. Esto te permite hacer una demo completa sin depender de conexión a Azure ni credenciales.

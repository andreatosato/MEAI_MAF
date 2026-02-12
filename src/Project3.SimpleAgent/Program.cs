// ============================================================================
// Progetto 3 - Esempio Semplice di Microsoft Agent Framework
// ============================================================================
// Questo progetto dimostra le basi di Microsoft Agent Framework (MAF).
// Crea un agente AI semplice basato su ChatClientAgent e lo espone via API
// con Swagger per il testing interattivo.
//
// Step 1: Configurare il client OpenAI (Azure OpenAI o OpenAI)
// Step 2: Creare un AIAgent con istruzioni personalizzate
// Step 3: Esporre l'agente tramite endpoint API
// Step 4: Testare con Swagger UI
// ============================================================================

using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

var builder = WebApplication.CreateBuilder(args);

// Step 1: Aggiungere i servizi Aspire (service discovery, health checks, telemetria)
builder.AddServiceDefaults();

// Step 2: Configurare Swagger per il testing delle API
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Progetto 3 - Simple Agent API", Version = "v1" });
});

// Step 3: Registrare il client OpenAI come servizio singleton.
// L'agente utilizza IChatClient come astrazione per comunicare con il modello LLM.
// Configura le variabili d'ambiente: AZURE_OPENAI_ENDPOINT e AZURE_OPENAI_DEPLOYMENT
builder.Services.AddSingleton<IChatClient>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var endpoint = config["AZURE_OPENAI_ENDPOINT"]
        ?? throw new InvalidOperationException("Configura AZURE_OPENAI_ENDPOINT in appsettings o variabili d'ambiente.");
    var deployment = config["AZURE_OPENAI_DEPLOYMENT"] ?? "gpt-4o-mini";

    // Usa Azure Identity per autenticazione senza chiavi (DefaultAzureCredential)
    return new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential())
        .GetChatClient(deployment)
        .AsIChatClient();
});

var app = builder.Build();

// Step 4: Abilitare Swagger UI
app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Simple Agent API v1"));

app.MapDefaultEndpoints();

// ============================================================================
// Endpoint: POST /api/chat
// Invia un messaggio all'agente e ricevi la risposta
// ============================================================================
app.MapPost("/api/chat", async (ChatRequest request, IChatClient chatClient) =>
{
    // Step 5: Creare un agente con istruzioni di sistema
    // ChatClientAgent è il tipo principale per creare agenti basati su LLM
    var agent = new ChatClientAgent(
        chatClient,
        name: "AssistenteWorkshop",
        instructions: "Sei un assistente tecnico esperto in .NET e Microsoft Agent Framework. " +
                      "Rispondi in modo chiaro e conciso in italiano. " +
                      "Fornisci esempi di codice quando possibile."
    );

    // Step 6: Eseguire l'agente con il messaggio dell'utente
    // RunAsync invia il messaggio e restituisce la risposta completa
    var response = await agent.RunAsync(request.Message);

    return Results.Ok(new ChatResponse(response.Text ?? "Nessuna risposta disponibile."));
})
.WithName("Chat")
.WithOpenApi()
.Produces<ChatResponse>(200)
.WithDescription("Invia un messaggio all'agente AI e ricevi la risposta.");

// ============================================================================
// Endpoint: POST /api/chat-with-history
// Supporta conversazioni multi-turno con cronologia
// ============================================================================
app.MapPost("/api/chat-with-history", async (ChatWithHistoryRequest request, IChatClient chatClient) =>
{
    // Step 7: Creare un agente con contesto personalizzato
    var agent = new ChatClientAgent(
        chatClient,
        name: "AssistenteConMemoria",
        instructions: request.SystemPrompt ?? "Sei un assistente tecnico esperto. Rispondi in italiano."
    );

    // Step 8: Costruire il prompt con la cronologia dei messaggi
    // Poiché ChatClientAgentSession ha un costruttore interno,
    // includiamo la cronologia direttamente nel prompt dell'agente
    var historyContext = "";
    if (request.History is { Count: > 0 })
    {
        historyContext = "Cronologia della conversazione precedente:\n" +
            string.Join("\n", request.History.Select(m =>
                $"[{m.Role}]: {m.Content}")) + "\n\n";
    }

    // Step 9: Eseguire l'agente con il messaggio dell'utente (e contesto)
    var fullMessage = historyContext + request.Message;
    var response = await agent.RunAsync(fullMessage);

    return Results.Ok(new ChatResponse(response.Text ?? "Nessuna risposta disponibile."));
})
.WithName("ChatWithHistory")
.WithOpenApi()
.Produces<ChatResponse>(200)
.WithDescription("Invia un messaggio con cronologia della conversazione.");

// ============================================================================
// Endpoint: GET /api/info
// Informazioni sull'agente e la configurazione
// ============================================================================
app.MapGet("/api/info", () =>
{
    return Results.Ok(new
    {
        Progetto = "Progetto 3 - Esempio Semplice MAF",
        Framework = "Microsoft Agent Framework (Preview)",
        Descrizione = "Questo esempio dimostra come creare un agente AI semplice con MAF",
        Endpoints = new[]
        {
            "POST /api/chat - Chat semplice con l'agente",
            "POST /api/chat-with-history - Chat con cronologia conversazione",
            "GET /api/info - Informazioni sull'agente"
        }
    });
})
.WithName("Info")
.WithOpenApi();

app.Run();

// ============================================================================
// Modelli di richiesta e risposta
// ============================================================================

/// <summary>Richiesta di chat semplice</summary>
record ChatRequest(string Message);

/// <summary>Risposta dell'agente</summary>
record ChatResponse(string Response);

/// <summary>Messaggio nella cronologia</summary>
record HistoryMessage(string Role, string Content);

/// <summary>Richiesta di chat con cronologia</summary>
record ChatWithHistoryRequest(
    string Message,
    string? SystemPrompt = null,
    List<HistoryMessage>? History = null);

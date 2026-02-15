// ============================================================================
// Progetto 3 - Esempio Semplice di Microsoft Agent Framework
// ============================================================================
// Questo progetto dimostra le basi di Microsoft Agent Framework (MAF).
// Crea un agente AI semplice basato su ChatClientAgent e lo espone via API
// con Swagger per il testing interattivo.
//
// Step 1: Configurare il client OpenAI
// Step 2: Creare un AIAgent con istruzioni personalizzate
// Step 3: Esporre l'agente tramite endpoint API
// ============================================================================

using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

var builder = WebApplication.CreateBuilder(args);

// Step 1: Aggiungere i servizi Aspire
builder.AddServiceDefaults();

// Step 2: Configurare Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Progetto 3 - Simple Agent API", Version = "v1" });
});

// Step 3: Registrare il client OpenAI
builder.Services.AddSingleton<IChatClient>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var endpoint = config["AZURE_OPENAI_ENDPOINT"]
        ?? throw new InvalidOperationException("Configura AZURE_OPENAI_ENDPOINT.");
    var deployment = config["AZURE_OPENAI_DEPLOYMENT"] ?? "gpt-4o-mini";

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
    // Creare un agente con istruzioni di sistema
    var agent = new ChatClientAgent(
        chatClient,
        name: "AssistenteWorkshop",
        instructions: "Sei un assistente tecnico esperto in .NET e Microsoft Agent Framework. " +
                      "Rispondi in modo chiaro e conciso in italiano. " +
                      "Fornisci esempi di codice quando possibile."
    );

    // Eseguire l'agente con il messaggio dell'utente
    var response = await agent.RunAsync(request.Message);

    return Results.Ok(new ChatResponse(response.Text ?? "Nessuna risposta disponibile."));
})
.WithName("Chat")
.WithOpenApi()
.Produces<ChatResponse>(200)
.WithDescription("Invia un messaggio all'agente AI e ricevi la risposta.");

// ============================================================================
// Endpoint: GET /api/info
// ============================================================================
app.MapGet("/api/info", () =>
{
    return Results.Ok(new
    {
        Progetto = "Progetto 3 - Simple Agent",
        Framework = "Microsoft Agent Framework",
        Descrizione = "Agente AI semplice per chat interattiva",
        Endpoints = new[]
        {
            "POST /api/chat - Chat con l'agente AI",
            "GET /api/info - Informazioni sull'agente"
        }
    });
})
.WithName("Info")
.WithOpenApi();

app.Run();

// ============================================================================
// Modelli
// ============================================================================
record ChatRequest(string Message);
record ChatResponse(string Response);

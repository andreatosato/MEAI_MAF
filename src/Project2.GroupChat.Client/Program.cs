// ============================================================================
// Progetto 2 - GroupChat Client (A2A Client)
// ============================================================================
// Client A2A che si connette al GroupChat Server tramite il protocollo
// Agent-to-Agent (A2A) e permette di inviare richieste al team di agenti.
//
// Il client:
// 1. Scopre l'agente remoto tramite l'Agent Card (/.well-known/agent.json)
// 2. Invia richieste al server A2A
// 3. Riceve le risposte dal team GroupChat
//
// Step 1: Configurare il client HTTP per connettersi al server A2A
// Step 2: Implementare la discovery dell'agente
// Step 3: Implementare l'invio di richieste
// ============================================================================

using Microsoft.Agents.AI;
using A2A;

var builder = WebApplication.CreateBuilder(args);

// Step 1: Aggiungere i servizi Aspire
builder.AddServiceDefaults();

// Step 2: Configurare Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Progetto 2 - GroupChat A2A Client", Version = "v1" });
});

// Step 3: Registrare HttpClient per comunicare con il server A2A
// Aspire service discovery risolve automaticamente "groupchat-server"
builder.Services.AddHttpClient("A2AServer", client =>
{
    var serverUrl = builder.Configuration["A2A_SERVER_URL"] ?? "https+http://groupchat-server";
    client.BaseAddress = new Uri(serverUrl);
});

var app = builder.Build();

// Step 4: Abilitare Swagger UI
app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "GroupChat A2A Client v1"));

app.MapDefaultEndpoints();

// ============================================================================
// Endpoint: POST /api/ask
// Invia una richiesta al server A2A GroupChat
// ============================================================================
app.MapPost("/api/ask", async (
    A2AClientRequest request,
    IHttpClientFactory httpClientFactory,
    ILogger<Program> logger) =>
{
    // Step 5: Creare il client A2A per comunicare con il server
    var httpClient = httpClientFactory.CreateClient("A2AServer");
    var baseUrl = httpClient.BaseAddress
        ?? throw new InvalidOperationException("URL del server A2A non configurato.");

    try
    {
        // Step 6: Risolvere l'agente remoto tramite A2A CardResolver
        // A2ACardResolver scopre automaticamente le capacità dell'agente
        var cardResolver = new A2ACardResolver(baseUrl, httpClient);
        var agentCard = await cardResolver.GetAgentCardAsync();

        logger.LogInformation("Agente remoto trovato: {Name}", agentCard.Name);

        // Step 7: Creare il client A2A e inviare il messaggio
        var a2aClient = new A2AClient(baseUrl, httpClient);
        var message = new A2A.AgentMessage
        {
            Role = A2A.MessageRole.User,
            Parts = [new A2A.TextPart { Text = request.Message }]
        };

        var result = await a2aClient.SendMessageAsync(message);
        var responseText = "Risposta ricevuta dal server A2A.";

        // Estrarre il testo dalla risposta
        if (result is A2A.AgentMessage responseMsg)
        {
            responseText = string.Join(" ", responseMsg.Parts?.OfType<A2A.TextPart>().Select(p => p.Text) ?? []);
        }
        else if (result is A2A.AgentTask task)
        {
            var artifacts = task.Artifacts ?? [];
            var parts = artifacts.SelectMany(a => a.Parts ?? []).OfType<A2A.TextPart>();
            responseText = string.Join(" ", parts.Select(p => p.Text));
            if (string.IsNullOrEmpty(responseText))
                responseText = $"Task creato con ID: {task.Id}, Stato: {task.Status.State}";
        }

        return Results.Ok(new A2AClientResponse(
            request.Message,
            responseText,
            true,
            null));
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Errore nella comunicazione A2A");
        return Results.Ok(new A2AClientResponse(
            request.Message,
            null,
            false,
            $"Errore di comunicazione con il server A2A: {ex.Message}"));
    }
})
.WithName("AskGroupChat")
.WithOpenApi()
.Produces<A2AClientResponse>(200)
.WithDescription("Invia una richiesta al server A2A GroupChat e ricevi la risposta del team.");

// ============================================================================
// Endpoint: GET /api/discover
// Scopri le informazioni sull'agente remoto (Agent Card)
// ============================================================================
app.MapGet("/api/discover", async (
    IHttpClientFactory httpClientFactory,
    ILogger<Program> logger) =>
{
    var httpClient = httpClientFactory.CreateClient("A2AServer");

    try
    {
        // Step 8: Ottenere l'Agent Card dal server A2A
        var cardResponse = await httpClient.GetStringAsync("/.well-known/agent.json");
        return Results.Ok(new
        {
            Success = true,
            AgentCard = cardResponse,
            Message = "Agent Card recuperata con successo dal server A2A."
        });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Errore durante la discovery");
        return Results.Ok(new
        {
            Success = false,
            AgentCard = (string?)null,
            Message = $"Errore durante la discovery: {ex.Message}"
        });
    }
})
.WithName("DiscoverAgent")
.WithOpenApi()
.WithDescription("Scopri le informazioni sull'agente remoto tramite A2A Agent Card.");

// ============================================================================
// Endpoint: GET /api/info
// ============================================================================
app.MapGet("/api/info", () =>
{
    return Results.Ok(new
    {
        Progetto = "Progetto 2 - GroupChat A2A Client",
        Descrizione = "Client A2A che comunica con il GroupChat Server",
        Protocollo = "A2A (Agent-to-Agent) - JSON-RPC 2.0 over HTTPS",
        Endpoints = new[]
        {
            "POST /api/ask - Invia una richiesta al GroupChat via A2A",
            "GET /api/discover - Scopri l'Agent Card del server",
            "GET /api/info - Informazioni sul client"
        }
    });
})
.WithName("Info")
.WithOpenApi();

app.Run();

// ============================================================================
// Modelli
// ============================================================================
record A2AClientRequest(string Message);
record A2AClientResponse(string Question, string? Response, bool Success, string? Error);

// ============================================================================
// Progetto 2 - GroupChat Server (A2A Server)
// ============================================================================
// Server A2A che espone un workflow GroupChat con 3 agenti collaborativi:
// 1. Analista: Analizza i requisiti e propone soluzioni
// 2. Sviluppatore: Scrive il codice basandosi sulle analisi
// 3. Revisore: Revisiona il codice e suggerisce miglioramenti
//
// Il server espone l'agente tramite il protocollo A2A (Agent-to-Agent)
// permettendo ad altri agenti/client di interagire con il GroupChat.
//
// Step 1: Configurare i 3 agenti
// Step 2: Creare il workflow GroupChat
// Step 3: Esporre tramite A2A protocol
// ============================================================================

using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

var builder = WebApplication.CreateBuilder(args);

// Step 1: Aggiungere i servizi Aspire
builder.AddServiceDefaults();

// Step 2: Configurare Swagger per debugging/info
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Progetto 2 - GroupChat A2A Server", Version = "v1" });
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

// Step 4: Configurare l'agente hosted con workflow GroupChat
// AddAIAgent registra un agente nel sistema di hosting
var hostedAgent = builder.AddAIAgent("groupchat-agent", (sp, sessionId) =>
{
    var chatClient = sp.GetRequiredService<IChatClient>();

    // Step 5: Creare i 3 agenti specializzati
    // Agente 1 - Analista: analizza requisiti e propone architetture
    var analista = new ChatClientAgent(
        chatClient,
        name: "Analista",
        instructions: """
            Sei un analista software esperto. Il tuo ruolo è:
            - Analizzare i requisiti forniti dall'utente
            - Proporre un'architettura e un design pattern appropriato
            - Identificare potenziali rischi e sfide
            - Fornire una specifica tecnica chiara per lo Sviluppatore
            Rispondi sempre in italiano. Sii conciso ma preciso.
            """
    );

    // Agente 2 - Sviluppatore: scrive il codice
    var sviluppatore = new ChatClientAgent(
        chatClient,
        name: "Sviluppatore",
        instructions: """
            Sei uno sviluppatore .NET senior esperto. Il tuo ruolo è:
            - Scrivere codice C# pulito e ben strutturato
            - Seguire le best practice e i design pattern suggeriti dall'Analista
            - Implementare la soluzione in modo completo e funzionante
            - Utilizzare i namespace e le API corrette di .NET
            Rispondi sempre in italiano. Includi commenti nel codice.
            """
    );

    // Agente 3 - Revisore: revisiona il codice
    var revisore = new ChatClientAgent(
        chatClient,
        name: "Revisore",
        instructions: """
            Sei un code reviewer esperto e meticoloso. Il tuo ruolo è:
            - Revisionare il codice prodotto dallo Sviluppatore
            - Verificare correttezza, performance e sicurezza
            - Suggerire miglioramenti e refactoring
            - Dare un giudizio finale (Approvato / Richieste modifiche)
            Rispondi sempre in italiano. Sii costruttivo nei feedback.
            """
    );

    // Step 6: Creare il workflow GroupChat con orchestrazione round-robin.
    // AgentWorkflowBuilder.CreateGroupChatBuilderWith crea il builder con un manager factory.
    var agents = new AIAgent[] { analista, sviluppatore, revisore };
    var workflow = AgentWorkflowBuilder
        .CreateGroupChatBuilderWith(agentList => new RoundRobinGroupChatManager(agentList))
        .AddParticipants(agents)
        .Build();

    // Step 7: Wrappare il workflow come AIAgent per l'hosting
    return workflow.AsAgent(name: "GroupChatTeam");
})
.WithInMemorySessionStore();

var app = builder.Build();

// Step 8: Abilitare Swagger UI
app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "GroupChat A2A Server v1"));

app.MapDefaultEndpoints();

// Step 9: Esporre l'agente tramite il protocollo A2A
// MapA2A mappa gli endpoint A2A standard (/.well-known/agent.json, /a2a)
app.MapA2A(hostedAgent, "groupchat");

// ============================================================================
// Endpoint: POST /api/groupchat
// Permette di testare il GroupChat direttamente via API REST
// ============================================================================
app.MapPost("/api/groupchat", async (GroupChatRequest request, IServiceProvider sp) =>
{
    var chatClient = sp.GetRequiredService<IChatClient>();

    // Ricreare gli agenti per la richiesta REST diretta
    var analista = new ChatClientAgent(chatClient, name: "Analista",
        instructions: "Sei un analista software. Analizza i requisiti e proponi un'architettura. Rispondi in italiano.");
    var sviluppatore = new ChatClientAgent(chatClient, name: "Sviluppatore",
        instructions: "Sei uno sviluppatore .NET. Scrivi codice C# basandoti sull'analisi. Rispondi in italiano.");
    var revisore = new ChatClientAgent(chatClient, name: "Revisore",
        instructions: "Sei un code reviewer. Revisiona il codice e dai feedback. Rispondi in italiano.");

    // Eseguire il workflow GroupChat
    var agents = new AIAgent[] { analista, sviluppatore, revisore };
    var workflow = AgentWorkflowBuilder
        .CreateGroupChatBuilderWith(agentList => new RoundRobinGroupChatManager(agentList))
        .AddParticipants(agents)
        .Build();

    var agent = workflow.AsAgent(name: "GroupChatTeam");
    var response = await agent.RunAsync(request.Message);

    return Results.Ok(new GroupChatResponse(
        request.Message,
        response.Text ?? "Nessuna risposta.",
        response.Messages?.Select(m => new AgentMessage(
            m.AuthorName ?? "Sistema",
            m.Text ?? "")).ToList() ?? []
    ));
})
.WithName("GroupChat")
.WithOpenApi()
.Produces<GroupChatResponse>(200)
.WithDescription("Invia una richiesta al team GroupChat con 3 agenti (Analista, Sviluppatore, Revisore).");

// ============================================================================
// Endpoint: GET /api/info
// ============================================================================
app.MapGet("/api/info", () =>
{
    return Results.Ok(new
    {
        Progetto = "Progetto 2 - GroupChat A2A Server",
        Descrizione = "Server A2A con workflow GroupChat a 3 agenti",
        Agenti = new[]
        {
            "Analista - Analizza requisiti e propone architetture",
            "Sviluppatore - Scrive codice C# basandosi sull'analisi",
            "Revisore - Revisiona il codice e suggerisce miglioramenti"
        },
        Endpoints = new[]
        {
            "POST /api/groupchat - Test diretto del GroupChat",
            "GET /.well-known/agent.json - Agent Card (A2A discovery)",
            "POST /a2a - Endpoint A2A protocol"
        }
    });
})
.WithName("Info")
.WithOpenApi();

app.Run();

// ============================================================================
// Modelli
// ============================================================================
record GroupChatRequest(string Message, int? MaxTurns = 6);
record GroupChatResponse(string Question, string FinalResponse, List<AgentMessage> Conversation);
record AgentMessage(string Agent, string Message);

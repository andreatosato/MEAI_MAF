using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI.Chat;
using Project3.SimpleAgent;

var builder = WebApplication.CreateBuilder(args);

// Step 1: Aggiungere i servizi Aspire
builder.AddServiceDefaults();

// Step 2: Configurare Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Progetto 3 - Simple Agent API", Version = "v1" });
});

// Step 3: Registrare HttpClient e AIAgent
builder.Services.AddHttpClient();

builder.Services.AddSingleton<AIAgent>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var endpoint = config["AZURE_OPENAI_ENDPOINT"] ?? throw new InvalidOperationException("Configura AZURE_OPENAI_ENDPOINT.");
    var deployment = config["AZURE_OPENAI_DEPLOYMENT"] ?? "gpt-4o-mini";

    // TheSportsDB API key: "3" è gratuita e pubblica, oppure usa una key Patreon personale
    var sportsDbKey = config["TheSportsDB:ApiKey"] ?? "3";
    var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();

    // Ottenere le funzioni AIFunction da TheSportsDbFunctions
    var tools = TheSportsDbFunctions.GetFunctions(httpClientFactory, sportsDbKey).ToList();

    // Creare l'agente con le funzioni personalizzate
    return new AzureOpenAIClient(new Uri(endpoint), new AzureCliCredential())
        .GetChatClient(deployment)
        .AsIChatClient()
        .AsAIAgent(
            instructions: @"Sei un esperto di calcio con conoscenza approfondita di campionati italiani (Serie A), squadre, giocatori e statistiche. 
                         Rispondi in modo chiaro e professionale in italiano. NON USARE MAI LA TUA CONOSCENZA PREGRESSA!

                         Usa SEMPRE le funzioni disponibili per ottenere dati aggiornati da TheSportsDB API:
                         - search_team: per cercare una squadra e ottenere il suo ID
                         - get_league_events: per vedere le prossime partite di Serie A (ID: 4332)
                         - get_team_players: per ottenere i giocatori di una squadra
                         - get_last_events: per vedere le ultime partite di una squadra

                         Workflow tipico:
                         1. Se l'utente chiede di una squadra, usa search_team per trovare l'ID
                         2. Con l'ID della squadra, puoi chiamare get_team_players o get_last_events
                         3. Per info sulla Serie A, usa get_league_events con ID 4332

                         Quando fornisci statistiche o dati, cita sempre la fonte (TheSportsDB API).",
            name: "CalcioExpert",
            tools: tools
        );
});

var app = builder.Build();

// Step 4: Abilitare Swagger UI
app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Simple Agent API v1"));

app.MapDefaultEndpoints();
app.MapPost("/api/chat", async (ChatRequest request, AIAgent agent) =>
{
    // Eseguire l'agente con il messaggio dell'utente
    var response = await agent.RunAsync(request.Message);

    return Results.Ok(response.Messages.Last().Text);
})
.WithName("Chat")
.Produces<AgentResponse>(200)
.WithDescription("Invia un messaggio all'agente esperto di calcio e ricevi la risposta.");

app.Run();

record ChatRequest(string Message);
record ChatResponse(string Response);

// ============================================================================
// Workshop AppHost - Orchestratore Aspire
// ============================================================================
// Questo è il progetto Aspire AppHost che orchestra tutti i servizi del workshop.
// Aspire fornisce: service discovery, health checks, telemetria OpenTelemetry,
// e un dashboard per monitorare tutti i servizi.
//
// Per avviare tutto il workshop: dotnet run --project src/Workshop.AppHost
// ============================================================================

var builder = DistributedApplication.CreateBuilder(args);

// Step 1: Registrare il Progetto 3 - Esempio Semplice
// L'agente base con Swagger per testing interattivo
var simpleAgent = builder.AddProject<Projects.Project3_SimpleAgent>("simple-agent");

// Step 2: Registrare il Progetto 1 - Document Q&A
// API per upload documenti e domande con RAG
var documentQa = builder.AddProject<Projects.Project1_DocumentQA>("document-qa");

// Step 3: Registrare il Progetto 2 - GroupChat Server (A2A)
// Server con 3 agenti collaborativi esposto via A2A
var groupChatServer = builder.AddProject<Projects.Project2_GroupChat_Server>("groupchat-server");

// Step 4: Registrare il Progetto 2 - GroupChat Client (A2A)
// Client che comunica con il server via protocollo A2A
var groupChatClient = builder.AddProject<Projects.Project2_GroupChat_Client>("groupchat-client")
    .WithReference(groupChatServer);

builder.Build().Run();

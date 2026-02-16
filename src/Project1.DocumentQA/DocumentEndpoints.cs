using System.ComponentModel.DataAnnotations;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Project1.DocumentQA;

public static class DocumentEndpoints
{
    public static IEndpointRouteBuilder MapDocumentEndpoints(this IEndpointRouteBuilder app, string uploadPath)
    {
        // ============================================================================
        // Endpoint: POST /api/documents/upload
        // Carica un file nel repository documentale
        // ============================================================================
        app.MapPost("/api/documents/upload", async (
            IFormFile file,
            DocumentRepository repository,
            ILogger<Program> logger) =>
        {
            // Step 9: Validare il file caricato
            if (file.Length == 0)
                return Results.BadRequest("Il file è vuoto.");

            var allowedExtensions = new[] { ".pdf", ".docx", ".txt", ".md" };
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(extension))
                return Results.BadRequest($"Formato non supportato. Formati accettati: {string.Join(", ", allowedExtensions)}");

            // Step 10: Salvare il file nella directory uploads
            var filePath = Path.Combine(uploadPath, $"{Guid.NewGuid()}{extension}");
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            logger.LogInformation("File caricato: {FileName} -> {FilePath}", file.FileName, filePath);

            // Step 11: Processare il documento con data ingestion (chunking)
            var chunkCount = await repository.IngestDocumentAsync(filePath, file.FileName);

            return Results.Ok(new UploadResponse(
                file.FileName,
                chunkCount,
                $"Documento caricato e indicizzato con successo in {chunkCount} chunk."));
        })
        .WithName("UploadDocument")
        .DisableAntiforgery()
        .Produces<UploadResponse>(200)
        .WithDescription("Carica un file (PDF, DOCX, TXT, MD) nel repository documentale.");

        // ============================================================================
        // Endpoint: POST /api/documents/ask
        // Fai una domanda sul contenuto dei documenti caricati
        // ============================================================================
        app.MapPost("/api/documents/ask", async (
            QuestionRequest request,
            DocumentRepository repository,
            IChatClient chatClient) =>
        {
            // Step 12: Cercare i chunk rilevanti nel vector store
            var relevantChunks = await repository.SearchAsync(request.Question, maxResults: 5);

            if (relevantChunks.Count == 0)
                return Results.Ok(new AnswerResponse(
                    request.Question,
                    "Nessun documento trovato. Carica prima dei documenti tramite /api/documents/upload.",
                    Array.Empty<string>()));

            // Step 13: Creare il contesto RAG con i chunk trovati
            var context = string.Join("\n\n---\n\n", relevantChunks.Select(c => c.Text));

            // Step 14: Creare un agente specializzato in Q&A documentale
            var agent = new ChatClientAgent(
                chatClient,
                name: "DocumentQAAgent",
                instructions: "Sei un assistente esperto nell'analisi documentale. " +
                              "Rispondi alle domande basandoti ESCLUSIVAMENTE sul contesto fornito. " +
                              "Se il contesto non contiene informazioni sufficienti, dillo chiaramente. " +
                              "Rispondi in italiano."
            );

            // Step 15: Costruire il prompt con contesto RAG
            var ragPrompt = $"""
                Contesto dai documenti:
                {context}

                Domanda dell'utente: {request.Question}

                Rispondi basandoti sul contesto fornito.
                """;

            var response = await agent.RunAsync(ragPrompt);

            return Results.Ok(new AnswerResponse(
                request.Question,
                response.Text ?? "Nessuna risposta disponibile.",
                relevantChunks.Select(c => c.Source).Distinct().ToArray()));
        })
        .WithName("AskQuestion")
        .Produces<AnswerResponse>(200)
        .WithDescription("Fai una domanda sul contenuto dei documenti caricati.");

        // ============================================================================
        // Endpoint: GET /api/documents
        // Lista dei documenti caricati
        // ============================================================================
        app.MapGet("/api/documents", (DocumentRepository repository) =>
        {
            return Results.Ok(repository.GetDocuments());
        })
        .WithName("ListDocuments")
        .WithDescription("Elenca tutti i documenti caricati nel repository.");

        // ============================================================================
        // Endpoint: GET /api/info
        // Informazioni sull'API e suggerimenti PDF
        // ============================================================================
        app.MapGet("/api/info", () =>
        {
            return Results.Ok(new
            {
                Progetto = "Progetto 1 - Document Q&A API",
                Descrizione = "API per caricare documenti e fare domande usando RAG",
                SuggerimentiPDF = new[]
                {
                    "https://dotnet.microsoft.com/download - Documentazione .NET (scarica PDF dalla docs)",
                    "https://arxiv.org/abs/2005.11401 - Paper RAG (Retrieval-Augmented Generation)",
                    "https://arxiv.org/abs/1706.03762 - Paper 'Attention Is All You Need' (Transformer)",
                    "Qualsiasi PDF tecnico, manuale o documentazione aziendale",
                    "Report annuali di aziende pubbliche (es. Microsoft Annual Report)"
                },
                Endpoints = new[]
                {
                    "POST /api/documents/upload - Carica un documento",
                    "POST /api/documents/ask - Fai una domanda",
                    "GET /api/documents - Lista documenti"
                }
            });
        })
        .WithName("Info")
        .WithOpenApi();

        return app;
    }
}

// ============================================================================
// Modelli
// ============================================================================
record UploadResponse(string FileName, int ChunkCount, string Message);
record QuestionRequest([Required] string Question);
record AnswerResponse(string Question, string Answer, string[] Sources);

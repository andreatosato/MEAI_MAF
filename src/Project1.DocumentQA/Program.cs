// ============================================================================
// Progetto 1 - Document Q&A API
// ============================================================================
// API con Swagger per caricare file (PDF, DOCX, TXT) e fare domande
// sul repository documentale.
// Utilizza Microsoft.Extensions.DataIngestion per il chunking dei documenti
// e Microsoft Agent Framework per le risposte basate su AI.
//
// Step 1: Configurare i servizi (OpenAI, VectorStore, Swagger)
// Step 2: Implementare l'upload dei documenti con data ingestion
// Step 3: Implementare il chunking e l'indicizzazione
// Step 4: Implementare l'endpoint di domanda/risposta con RAG
// ============================================================================

using System.ComponentModel.DataAnnotations;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DataIngestion;
using Microsoft.Extensions.DataIngestion.Chunkers;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Connectors.InMemory;

var builder = WebApplication.CreateBuilder(args);

// Step 1: Aggiungere i servizi Aspire
builder.AddServiceDefaults();

// Step 2: Configurare Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Progetto 1 - Document Q&A API", Version = "v1" });
});

// Step 3: Configurare il client Azure OpenAI
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

// Step 4: Configurare l'embedding generator per la ricerca vettoriale
builder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var endpoint = config["AZURE_OPENAI_ENDPOINT"]
        ?? throw new InvalidOperationException("Configura AZURE_OPENAI_ENDPOINT.");
    var embeddingDeployment = config["AZURE_OPENAI_EMBEDDING_DEPLOYMENT"] ?? "text-embedding-ada-002";

    return new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential())
        .GetEmbeddingClient(embeddingDeployment)
        .AsIEmbeddingGenerator();
});

// Step 5: Configurare il VectorStore in memoria per lo storage dei chunk
builder.Services.AddSingleton<VectorStore>(new InMemoryVectorStore());

// Step 6: Registrare il servizio di gestione documenti
builder.Services.AddSingleton<DocumentRepository>();

// Step 7: Creare la directory per l'upload dei file
var uploadPath = Path.Combine(builder.Environment.ContentRootPath, "uploads");
Directory.CreateDirectory(uploadPath);

var app = builder.Build();

// Step 8: Abilitare Swagger UI
app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Document Q&A API v1"));

app.MapDefaultEndpoints();

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
.WithOpenApi()
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
.WithOpenApi()
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
.WithOpenApi()
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

app.Run();

// ============================================================================
// DocumentRepository - Servizio per gestione documenti e ricerca vettoriale
// ============================================================================
class DocumentRepository
{
    private readonly VectorStore _vectorStore;
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    private readonly ILogger<DocumentRepository> _logger;
    private readonly List<DocumentInfo> _documents = [];
    private VectorStoreCollection<string, DocumentChunk>? _collection;

    public DocumentRepository(
        VectorStore vectorStore,
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        ILogger<DocumentRepository> logger)
    {
        _vectorStore = vectorStore;
        _embeddingGenerator = embeddingGenerator;
        _logger = logger;
    }

    private async Task<VectorStoreCollection<string, DocumentChunk>> GetCollectionAsync()
    {
        if (_collection is not null)
            return _collection;

        _collection = _vectorStore.GetCollection<string, DocumentChunk>("documents");
        await _collection.EnsureCollectionExistsAsync();
        return _collection;
    }

    /// <summary>
    /// Processa un documento usando DataIngestion: legge il file, lo divide in chunk
    /// e li salva nel vector store.
    /// </summary>
    public async Task<int> IngestDocumentAsync(string filePath, string originalName)
    {
        _logger.LogInformation("Inizio ingestion per: {FileName}", originalName);

        // Leggi il contenuto del file come testo
        var content = await File.ReadAllTextAsync(filePath);

        // Usa il chunking basato su token per dividere il contenuto
        var chunks = ChunkText(content, maxChunkSize: 500, overlap: 50);

        var collection = await GetCollectionAsync();
        var chunkCount = 0;

        foreach (var chunkText in chunks)
        {
            // Genera l'embedding per ogni chunk
            var embedding = await _embeddingGenerator.GenerateAsync(chunkText);

            var chunk = new DocumentChunk
            {
                Id = Guid.NewGuid().ToString(),
                Text = chunkText,
                Source = originalName,
                Vector = embedding.Vector
            };

            await collection.UpsertAsync(chunk);
            chunkCount++;
        }

        _documents.Add(new DocumentInfo(originalName, filePath, chunkCount, DateTime.UtcNow));
        _logger.LogInformation("Ingestion completata: {FileName} -> {ChunkCount} chunk", originalName, chunkCount);

        return chunkCount;
    }

    /// <summary>
    /// Cerca i chunk più rilevanti per una query usando la ricerca vettoriale.
    /// </summary>
    public async Task<List<ChunkResult>> SearchAsync(string query, int maxResults = 5)
    {
        var collection = await GetCollectionAsync();
        var queryEmbedding = await _embeddingGenerator.GenerateAsync(query);

        var results = new List<ChunkResult>();
        await foreach (var result in collection.SearchAsync(queryEmbedding.Vector, top: maxResults))
        {
            if (result.Record is not null)
            {
                results.Add(new ChunkResult(result.Record.Text, result.Record.Source));
            }
        }

        return results;
    }

    public List<DocumentInfo> GetDocuments() => _documents;

    /// <summary>
    /// Divide il testo in chunk con sovrapposizione (overlap) per mantenere il contesto.
    /// </summary>
    private static List<string> ChunkText(string text, int maxChunkSize, int overlap)
    {
        var chunks = new List<string>();
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        for (int i = 0; i < words.Length; i += maxChunkSize - overlap)
        {
            var chunkWords = words.Skip(i).Take(maxChunkSize).ToArray();
            if (chunkWords.Length > 0)
            {
                chunks.Add(string.Join(' ', chunkWords));
            }
        }

        return chunks;
    }
}

// ============================================================================
// Modelli
// ============================================================================

/// <summary>Chunk di documento memorizzato nel vector store</summary>
class DocumentChunk
{
    [VectorStoreKey]
    public string Id { get; set; } = string.Empty;

    [VectorStoreData]
    public string Text { get; set; } = string.Empty;

    [VectorStoreData]
    public string Source { get; set; } = string.Empty;

    [VectorStoreVector(1536)]
    public ReadOnlyMemory<float> Vector { get; set; }
}

record DocumentInfo(string FileName, string FilePath, int ChunkCount, DateTime UploadedAt);
record UploadResponse(string FileName, int ChunkCount, string Message);
record QuestionRequest([Required] string Question);
record AnswerResponse(string Question, string Answer, string[] Sources);
record ChunkResult(string Text, string Source);

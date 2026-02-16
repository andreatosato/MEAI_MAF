using Microsoft.Extensions.AI;
using Microsoft.Extensions.DataIngestion;
using Microsoft.Extensions.VectorData;

namespace Project1.DocumentQA;

/// <summary>
/// Servizio per gestione documenti e ricerca vettoriale.
/// Usa MarkdownReader per MD/TXT e PdfDocumentReader per PDF (100% .NET).
/// Ogni sezione del documento diventa un chunk con embedding.
/// </summary>
public class DocumentRepository
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
    /// Processa un documento usando DataIngestion (solo .NET):
    /// PdfDocumentReader per PDF, MarkdownReader per MD/TXT,
    /// Processa ogni sezione direttamente senza chunker intermedio.
    /// </summary>
    public async Task<int> IngestDocumentAsync(string filePath, string originalName)
    {
        _logger.LogInformation("Inizio ingestion per: {FileName}", originalName);

        // Seleziona il reader appropriato in base all'estensione
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        IngestionDocumentReader reader = extension switch
        {
            ".pdf" => new PdfDocumentReader(),
            ".md" or ".txt" => new MarkdownReader(),
            _ => throw new NotSupportedException($"Formato file non supportato: {extension}")
        };

        var document = await reader.ReadAsync(new FileInfo(filePath));

        _logger.LogInformation("Documento letto: {Sections} sezioni trovate", document.Sections.Count);

        var collection = await GetCollectionAsync();
        var chunkCount = 0;

        // Processa ogni sezione direttamente (bypassando SectionChunker che ha problemi)
        foreach (var section in document.Sections)
        {
            var markdown = section.GetMarkdown();

            // Salta sezioni vuote
            if (string.IsNullOrWhiteSpace(markdown))
            {
                _logger.LogWarning("Sezione vuota saltata");
                continue;
            }

            _logger.LogInformation("Processando sezione {Count}: {Length} caratteri", 
                chunkCount + 1, markdown.Length);

            // Genera l'embedding per la sezione
            var embedding = await _embeddingGenerator.GenerateAsync(markdown);

            var chunk = new DocumentChunk
            {
                Id = Guid.NewGuid().ToString(),
                Text = markdown,
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
}

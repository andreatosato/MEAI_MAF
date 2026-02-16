using Microsoft.Extensions.AI;
using Microsoft.Extensions.DataIngestion;
using Microsoft.Extensions.VectorData;
using Microsoft.ML.Tokenizers;

namespace Project1.DocumentQA;

/// <summary>
/// Servizio per gestione documenti e ricerca vettoriale.
/// Usa MarkItDownReader e HeaderChunker da Microsoft.Extensions.DataIngestion.
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
    /// Processa un documento usando DataIngestion:
    /// MarkItDownReader per la lettura, HeaderChunker per il chunking,
    /// e salvataggio manuale nel VectorStore con embedding.
    /// </summary>
    public async Task<int> IngestDocumentAsync(string filePath, string originalName)
    {
        _logger.LogInformation("Inizio ingestion per: {FileName}", originalName);

        // Reader: converte qualsiasi formato (PDF, DOCX, TXT, MD) in IngestionDocument
        IngestionDocumentReader reader = new MarkItDownReader();
        var document = await reader.ReadAsync(new FileInfo(filePath));

        _logger.LogInformation("Documento letto: {Sections} sezioni trovate", document.Sections.Count);

        // Chunker: divide il documento in chunk basati su header con tokenizer GPT-4
        Tokenizer tokenizer = TiktokenTokenizer.CreateForModel("gpt-4");
        IngestionChunkerOptions chunkerOptions = new(tokenizer)
        {
            MaxTokensPerChunk = 500,
            OverlapTokens = 50
        };
        HeaderChunker chunker = new(chunkerOptions);

        // Processare il documento con il chunker
        var chunks = chunker.ProcessAsync(document);

        var collection = await GetCollectionAsync();
        var chunkCount = 0;

        await foreach (var ingestionChunk in chunks)
        {
            // Genera l'embedding per ogni chunk
            var embedding = await _embeddingGenerator.GenerateAsync(ingestionChunk.Content);

            var chunk = new DocumentChunk
            {
                Id = Guid.NewGuid().ToString(),
                Text = ingestionChunk.Content,
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

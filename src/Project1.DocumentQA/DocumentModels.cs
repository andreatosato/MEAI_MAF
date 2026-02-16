using Microsoft.Extensions.VectorData;

namespace Project1.DocumentQA;

/// <summary>Chunk di documento memorizzato nel vector store</summary>
public class DocumentChunk
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

public record DocumentInfo(string FileName, string FilePath, int ChunkCount, DateTime UploadedAt);
public record ChunkResult(string Text, string Source);

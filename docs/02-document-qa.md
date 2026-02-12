# Progetto 1 - Document Q&A con Data Ingestion

## Obiettivo

Creare un'API per caricare documenti e fare domande sul loro contenuto usando **RAG (Retrieval-Augmented Generation)** con `Microsoft.Extensions.DataIngestion` per il chunking.

## Durata stimata: ~25 minuti

## Prerequisiti

- .NET 10 SDK
- Account Azure con Azure OpenAI configurato
- Deployment modello chat (es. `gpt-4o-mini`) e embedding (es. `text-embedding-ada-002`)

## Architettura

```
┌─────────────┐     ┌──────────────┐     ┌─────────────┐
│  Upload File │────>│ Data Ingest  │────>│ Vector Store│
│  (PDF/TXT)   │     │ (Chunking)   │     │ (InMemory)  │
└─────────────┘     └──────────────┘     └──────┬──────┘
                                                 │
┌─────────────┐     ┌──────────────┐     ┌──────▼──────┐
│   Risposta   │<───│   AI Agent   │<───│   Ricerca   │
│   (RAG)      │     │  (MAF/LLM)  │     │ Vettoriale  │
└─────────────┘     └──────────────┘     └─────────────┘
```

## Concetti Chiave

| Concetto | Descrizione |
|---|---|
| **Data Ingestion** | Pipeline per leggere, dividere ed elaborare documenti |
| **Chunking** | Divisione del testo in pezzi (chunk) più piccoli per l'embedding |
| **Vector Store** | Database vettoriale per ricerca semantica |
| **RAG** | Tecnica che combina ricerca documentale + generazione AI |
| **Embedding** | Rappresentazione numerica del testo per la ricerca semantica |

## Pacchetti NuGet Utilizzati

```xml
<PackageReference Include="Microsoft.Extensions.DataIngestion" Version="10.3.0-preview.1.26109.11" />
<PackageReference Include="Microsoft.Extensions.DataIngestion.MarkItDown" Version="10.3.0-preview.1.26109.11" />
<PackageReference Include="Microsoft.Extensions.VectorData.Abstractions" Version="9.7.0" />
<PackageReference Include="Microsoft.SemanticKernel.Connectors.InMemory" Version="1.70.0-preview" />
```

## Step-by-Step

### Step 1: Configurare l'Embedding Generator

```csharp
builder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
{
    var endpoint = config["AZURE_OPENAI_ENDPOINT"];
    var embeddingDeployment = config["AZURE_OPENAI_EMBEDDING_DEPLOYMENT"] ?? "text-embedding-ada-002";

    return new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential())
        .GetEmbeddingClient(embeddingDeployment)
        .AsIEmbeddingGenerator();
});
```

### Step 2: Configurare il Vector Store

```csharp
builder.Services.AddSingleton<VectorStore>(new InMemoryVectorStore());
```

Il `InMemoryVectorStore` è perfetto per il workshop. In produzione si userebbe Azure AI Search, Qdrant, ecc.

### Step 3: Definire il modello del chunk

```csharp
class DocumentChunk
{
    [VectorStoreKey]
    public string Id { get; set; } = string.Empty;

    [VectorStoreData]
    public string Text { get; set; } = string.Empty;

    [VectorStoreData]
    public string Source { get; set; } = string.Empty;

    [VectorStoreVector(1536)]  // Dimensione embedding di ada-002
    public ReadOnlyMemory<float> Vector { get; set; }
}
```

### Step 4: Implementare il Chunking

Il testo viene diviso in chunk con sovrapposizione (overlap) per mantenere il contesto:

```csharp
private static List<string> ChunkText(string text, int maxChunkSize, int overlap)
{
    var chunks = new List<string>();
    var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);

    for (int i = 0; i < words.Length; i += maxChunkSize - overlap)
    {
        var chunkWords = words.Skip(i).Take(maxChunkSize).ToArray();
        if (chunkWords.Length > 0)
            chunks.Add(string.Join(' ', chunkWords));
    }
    return chunks;
}
```

### Step 5: Ingestion del documento

Per ogni chunk:
1. Genera l'embedding vettoriale
2. Salva nel Vector Store

### Step 6: Ricerca e risposta (RAG)

1. Genera l'embedding della domanda
2. Cerca i chunk più rilevanti nel Vector Store
3. Costruisci il prompt con il contesto trovato
4. Invia al ChatClientAgent per la risposta

### Step 7: Testare

```bash
# Avvia il progetto
dotnet run --project src/Project1.DocumentQA

# Carica un file
curl -X POST https://localhost:{porta}/api/documents/upload \
  -F "file=@mio-documento.txt"

# Fai una domanda
curl -X POST https://localhost:{porta}/api/documents/ask \
  -H "Content-Type: application/json" \
  -d '{"question": "Di cosa parla il documento?"}'
```

## Suggerimenti PDF per il Workshop

Ecco alcuni documenti PDF che puoi scaricare e usare per testare:

1. **Paper RAG originale** - [arxiv.org/abs/2005.11401](https://arxiv.org/abs/2005.11401)
   - Il paper che ha introdotto Retrieval-Augmented Generation
2. **"Attention Is All You Need"** - [arxiv.org/abs/1706.03762](https://arxiv.org/abs/1706.03762)
   - Il paper fondamentale sui Transformer
3. **Documentazione .NET** - [learn.microsoft.com](https://learn.microsoft.com/en-us/dotnet/)
   - Scarica pagine come PDF dal sito docs Microsoft
4. **Report annuali aziendali** - es. Microsoft Annual Report
5. **Qualsiasi PDF/TXT tecnico** della tua organizzazione

## Endpoint Disponibili

| Metodo | URL | Descrizione |
|---|---|---|
| POST | `/api/documents/upload` | Carica un documento (PDF, DOCX, TXT, MD) |
| POST | `/api/documents/ask` | Fai una domanda sul contenuto |
| GET | `/api/documents` | Lista documenti caricati |
| GET | `/api/info` | Info e suggerimenti |

## Prossimi Passi

- Prova con diversi tipi di documenti
- Sperimenta con domande specifiche vs. generiche
- Passa al [Progetto 2 - GroupChat](03-group-chat.md) per multi-agent

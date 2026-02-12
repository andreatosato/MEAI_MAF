# Progetto 3 - Esempio Semplice di Microsoft Agent Framework

## Obiettivo

Comprendere le basi di **Microsoft Agent Framework (MAF)** creando un agente AI semplice esposto tramite API con Swagger.

## Durata stimata: ~15 minuti

## Prerequisiti

- .NET 10 SDK
- Account Azure con Azure OpenAI configurato
- Deployment di un modello (es. `gpt-4o-mini`)

## Concetti Chiave

| Concetto | Descrizione |
|---|---|
| `IChatClient` | Astrazione unificata per comunicare con modelli LLM (OpenAI, Azure OpenAI, Ollama, ecc.) |
| `ChatClientAgent` | Tipo principale di MAF per creare agenti basati su un `IChatClient` |
| `AIAgent` | Classe base astratta per tutti gli agenti in MAF |
| `AgentResponse` | Risposta restituita dall'agente dopo l'esecuzione |

## Step-by-Step

### Step 1: Configurare il progetto

Il progetto è già configurato nella soluzione. I pacchetti NuGet necessari:

```xml
<PackageReference Include="Microsoft.Agents.AI" Version="1.0.0-preview.260209.1" />
<PackageReference Include="Microsoft.Agents.AI.OpenAI" Version="1.0.0-preview.260209.1" />
<PackageReference Include="Microsoft.Extensions.AI.OpenAI" Version="10.3.0" />
<PackageReference Include="Azure.AI.OpenAI" Version="2.8.0-beta.1" />
<PackageReference Include="Azure.Identity" Version="1.17.1" />
```

### Step 2: Registrare IChatClient

```csharp
builder.Services.AddSingleton<IChatClient>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var endpoint = config["AZURE_OPENAI_ENDPOINT"];
    var deployment = config["AZURE_OPENAI_DEPLOYMENT"] ?? "gpt-4o-mini";

    return new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential())
        .GetChatClient(deployment)
        .AsIChatClient();
});
```

**Nota**: `AsIChatClient()` è un metodo di estensione che converte un client OpenAI in `IChatClient`.

### Step 3: Creare un ChatClientAgent

```csharp
var agent = new ChatClientAgent(
    chatClient,
    name: "AssistenteWorkshop",
    instructions: "Sei un assistente tecnico esperto in .NET..."
);
```

Il `ChatClientAgent` accetta:
- **chatClient**: L'istanza `IChatClient` per la comunicazione con il modello
- **name**: Nome identificativo dell'agente
- **instructions**: Istruzioni di sistema (system prompt)

### Step 4: Eseguire l'agente

```csharp
var response = await agent.RunAsync("Qual è il tuo ruolo?");
Console.WriteLine(response.Text);
```

`RunAsync` invia il messaggio e restituisce un `AgentResponse` con il testo della risposta.

### Step 5: Testare con Swagger

1. Avvia il progetto:
   ```bash
   dotnet run --project src/Project3.SimpleAgent
   ```
2. Apri il browser su `https://localhost:{porta}/swagger`
3. Prova l'endpoint `POST /api/chat` con un messaggio
4. Prova l'endpoint `POST /api/chat-with-history` per una conversazione multi-turno

## Endpoint Disponibili

| Metodo | URL | Descrizione |
|---|---|---|
| POST | `/api/chat` | Chat semplice con l'agente |
| POST | `/api/chat-with-history` | Chat con cronologia conversazione |
| GET | `/api/info` | Informazioni sull'API |

## Esempio di Request/Response

**Request** (POST /api/chat):
```json
{
  "message": "Cos'è Microsoft Agent Framework?"
}
```

**Response**:
```json
{
  "response": "Microsoft Agent Framework (MAF) è un framework open-source di Microsoft per creare e orchestrare agenti AI in .NET..."
}
```

## Prossimi Passi

- Prova a modificare le istruzioni dell'agente per cambiarne il comportamento
- Sperimenta con conversazioni multi-turno usando `/api/chat-with-history`
- Passa al [Progetto 1 - Document Q&A](02-document-qa.md) per un caso d'uso più avanzato

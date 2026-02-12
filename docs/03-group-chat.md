# Progetto 2 - GroupChat con 3 Agenti e Protocollo A2A

## Obiettivo

Creare un sistema multi-agente con **GroupChat** (3 agenti collaborativi) e implementare la comunicazione **Agent-to-Agent (A2A)** con server e client separati.

## Durata stimata: ~20 minuti

## Prerequisiti

- .NET 10 SDK
- Account Azure con Azure OpenAI configurato
- Deployment di un modello (es. `gpt-4o-mini`)

## Architettura

```
┌────────────────────────────────────────────────────────────┐
│                    A2A Server                                │
│  ┌──────────┐   ┌──────────────┐   ┌──────────────┐        │
│  │ Analista  │──>│ Sviluppatore │──>│  Revisore    │        │
│  │ (Agent 1) │   │  (Agent 2)   │   │  (Agent 3)   │        │
│  └──────────┘   └──────────────┘   └──────────────┘        │
│        │              │                    │                  │
│        └──────────────┴────────────────────┘                 │
│                    GroupChat Manager                          │
│                   (Round-Robin)                               │
│                                                              │
│  Endpoint A2A: POST /a2a                                     │
│  Agent Card:   GET /.well-known/agent.json                   │
└──────────────────────────┬─────────────────────────────────┘
                           │ A2A Protocol (JSON-RPC 2.0)
┌──────────────────────────▼─────────────────────────────────┐
│                    A2A Client                                │
│                                                              │
│  1. Discovery: GET /.well-known/agent.json                   │
│  2. Send Message: POST /a2a                                  │
│  3. Receive Response                                         │
└────────────────────────────────────────────────────────────┘
```

## I 3 Agenti

| Agente | Ruolo | Responsabilità |
|---|---|---|
| **Analista** | Analisi requisiti | Analizzare il problema, proporre architettura, identificare rischi |
| **Sviluppatore** | Scrittura codice | Scrivere codice C# basandosi sull'analisi, seguire best practice |
| **Revisore** | Code review | Revisionare il codice, verificare correttezza, suggerire miglioramenti |

## Concetti Chiave

| Concetto | Descrizione |
|---|---|
| **GroupChat** | Workflow dove più agenti collaborano su un task |
| **GroupChatManager** | Controlla l'ordine degli agenti (es. Round-Robin) |
| **A2A Protocol** | Standard per comunicazione agent-to-agent via JSON-RPC 2.0 |
| **Agent Card** | Documento JSON che descrive le capacità dell'agente |
| **MapA2A** | Extension method per esporre un agente via A2A su ASP.NET Core |

## Step-by-Step

### Step 1: Creare i 3 agenti (Server)

```csharp
var analista = new ChatClientAgent(chatClient, name: "Analista",
    instructions: "Sei un analista software. Analizza requisiti...");

var sviluppatore = new ChatClientAgent(chatClient, name: "Sviluppatore",
    instructions: "Sei uno sviluppatore .NET. Scrivi codice C#...");

var revisore = new ChatClientAgent(chatClient, name: "Revisore",
    instructions: "Sei un code reviewer. Revisiona il codice...");
```

### Step 2: Creare il Workflow GroupChat

```csharp
var agents = new AIAgent[] { analista, sviluppatore, revisore };
var workflow = AgentWorkflowBuilder
    .CreateGroupChatBuilderWith(agentList => new RoundRobinGroupChatManager(agentList))
    .AddParticipants(agents)
    .Build();
```

- `AgentWorkflowBuilder.CreateGroupChatBuilderWith()` crea un builder con una factory per il manager
- `RoundRobinGroupChatManager` seleziona gli agenti in ordine ciclico
- `AddParticipants()` aggiunge tutti gli agenti al workflow

### Step 3: Ospitare come AIAgent

```csharp
var hostedAgent = builder.AddAIAgent("groupchat-agent", (sp, sessionId) =>
{
    // ... setup agenti e workflow ...
    return workflow.AsAgent(name: "GroupChatTeam");
})
.WithInMemorySessionStore();
```

### Step 4: Esporre via A2A

```csharp
app.MapA2A(hostedAgent, "groupchat");
```

Questo crea automaticamente:
- `GET /.well-known/agent.json` - Agent Card per la discovery
- `POST /a2a` - Endpoint A2A per la comunicazione

### Step 5: Implementare il Client A2A

```csharp
// Scoprire l'agente remoto
var cardResolver = new A2ACardResolver(baseUrl, httpClient);
var agentCard = await cardResolver.GetAgentCardAsync();

// Inviare un messaggio
var a2aClient = new A2AClient(baseUrl, httpClient);
var message = new AgentMessage
{
    Role = MessageRole.User,
    Parts = [new TextPart { Text = "Il mio messaggio" }]
};
var result = await a2aClient.SendMessageAsync(message);
```

### Step 6: Testare

#### Test diretto del server (REST API):
```bash
curl -X POST https://localhost:{porta}/api/groupchat \
  -H "Content-Type: application/json" \
  -d '{"message": "Crea un servizio REST per gestire una TODO list"}'
```

#### Test via Client A2A:
```bash
curl -X POST https://localhost:{porta-client}/api/ask \
  -H "Content-Type: application/json" \
  -d '{"message": "Progetta un sistema di autenticazione JWT"}'
```

#### Discovery dell'agente:
```bash
curl https://localhost:{porta-client}/api/discover
```

## Protocollo A2A in Dettaglio

Il protocollo **Agent-to-Agent (A2A)** è uno standard open per la comunicazione tra agenti:

1. **Discovery**: Il client richiede l'Agent Card (`/.well-known/agent.json`)
2. **Agent Card**: Contiene nome, descrizione, capacità e URL dell'agente
3. **Comunicazione**: Messaggi via JSON-RPC 2.0 su HTTPS
4. **Risposte**: L'agente può rispondere con `AgentMessage` (diretto) o `AgentTask` (asincrono)

## Endpoint Server

| Metodo | URL | Descrizione |
|---|---|---|
| POST | `/api/groupchat` | Test diretto del GroupChat |
| GET | `/.well-known/agent.json` | Agent Card (A2A discovery) |
| POST | `/a2a` | Endpoint A2A protocol |
| GET | `/api/info` | Informazioni sul server |

## Endpoint Client

| Metodo | URL | Descrizione |
|---|---|---|
| POST | `/api/ask` | Invia richiesta al server via A2A |
| GET | `/api/discover` | Scopri l'Agent Card del server |
| GET | `/api/info` | Informazioni sul client |

## Prossimi Passi

- Prova a cambiare il `GroupChatManager` (es. implementa uno basato su LLM)
- Aggiungi tools/funzioni agli agenti
- Sperimenta con diversi tipi di richieste
- Prova a collegare più server A2A tra loro

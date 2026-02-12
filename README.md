# Workshop: Microsoft Agent Framework (MAF) con .NET Aspire

Workshop pratico di 1 ora per studenti che introduce **Microsoft Agent Framework** e **Microsoft.Extensions.AI** attraverso 3 progetti gestiti con **.NET Aspire**.

## 🎯 Obiettivi del Workshop

1. Comprendere le basi di Microsoft Agent Framework
2. Implementare un sistema RAG con Data Ingestion
3. Creare workflow multi-agente con GroupChat e protocollo A2A

## 📋 Prerequisiti

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Account Azure con **Azure OpenAI** configurato
- Deployment modelli: `gpt-4o-mini` e `text-embedding-ada-002`

## 🏗️ Struttura del Workshop

| Progetto | Durata | Descrizione |
|---|---|---|
| [Progetto 3 - Simple Agent](docs/01-simple-agent.md) | ~15 min | Esempio base di MAF con Swagger |
| [Progetto 1 - Document Q&A](docs/02-document-qa.md) | ~25 min | API per upload documenti e Q&A con RAG |
| [Progetto 2 - GroupChat A2A](docs/03-group-chat.md) | ~20 min | 3 agenti in GroupChat + client/server A2A |

## 🚀 Quick Start

### 1. Configurazione

Copia le variabili d'ambiente nei file `appsettings.json` di ogni progetto, oppure usa `dotnet user-secrets`:

```bash
# Per ogni progetto nella cartella src/
dotnet user-secrets set "AZURE_OPENAI_ENDPOINT" "https://your-resource.openai.azure.com"
dotnet user-secrets set "AZURE_OPENAI_DEPLOYMENT" "gpt-4o-mini"
dotnet user-secrets set "AZURE_OPENAI_EMBEDDING_DEPLOYMENT" "text-embedding-ada-002"
```

### 2. Avvio con Aspire

```bash
dotnet run --project src/Workshop.AppHost
```

Questo avvia tutti i servizi e apre il **Dashboard Aspire** con:
- Health checks
- OpenTelemetry (traces, metrics, logs)
- Service discovery automatico

### 3. Testare i servizi

Ogni progetto ha Swagger UI disponibile:
- **Simple Agent**: `https://localhost:{porta}/swagger`
- **Document Q&A**: `https://localhost:{porta}/swagger`
- **GroupChat Server**: `https://localhost:{porta}/swagger`
- **GroupChat Client**: `https://localhost:{porta}/swagger`

## 📁 Struttura del Repository

```
├── Workshop.slnx                       # Solution file
├── docs/
│   ├── 01-simple-agent.md             # Guida Progetto 3
│   ├── 02-document-qa.md              # Guida Progetto 1
│   └── 03-group-chat.md               # Guida Progetto 2
└── src/
    ├── Workshop.AppHost/              # Aspire orchestrator
    ├── Workshop.ServiceDefaults/      # Shared Aspire services
    ├── Project1.DocumentQA/           # API Document Q&A con RAG
    ├── Project2.GroupChat.Server/     # GroupChat A2A Server
    ├── Project2.GroupChat.Client/     # A2A Client
    └── Project3.SimpleAgent/          # Esempio semplice MAF
```

## 📦 Pacchetti NuGet Principali

| Pacchetto | Descrizione |
|---|---|
| `Microsoft.Agents.AI` | Core di Microsoft Agent Framework |
| `Microsoft.Agents.AI.OpenAI` | Integrazione OpenAI/Azure OpenAI |
| `Microsoft.Agents.AI.Workflows` | Workflow multi-agente (GroupChat, Handoffs) |
| `Microsoft.Agents.AI.Hosting.A2A.AspNetCore` | Hosting A2A su ASP.NET Core |
| `Microsoft.Extensions.DataIngestion` | Pipeline di data ingestion per RAG |
| `Microsoft.Extensions.AI.OpenAI` | Astrazioni AI unificate |

## 📚 Risorse Utili

- [Microsoft Agent Framework Docs](https://learn.microsoft.com/en-us/agent-framework/)
- [Microsoft.Extensions.AI](https://learn.microsoft.com/en-us/dotnet/ai/)
- [Data Ingestion](https://learn.microsoft.com/en-us/dotnet/ai/conceptual/data-ingestion)
- [.NET Aspire](https://learn.microsoft.com/en-us/dotnet/aspire/)
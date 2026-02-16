using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Connectors.InMemory;
using Project1.DocumentQA;

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

    return new AzureOpenAIClient(new Uri(endpoint), new AzureCliCredential())
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

    return new AzureOpenAIClient(new Uri(endpoint), new AzureCliCredential())
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

app.MapDocumentEndpoints(uploadPath);

app.Run();

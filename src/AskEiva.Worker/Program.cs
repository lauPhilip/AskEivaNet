using System.Net.Http.Headers;
using System.Text;
using AskEiva.Domain.Repositories;
using AskEiva.Domain.Services;
using AskEiva.Infrastructure.Repositories;
using AskEiva.Infrastructure.Services;
using AskEiva.Worker;

var builder = Host.CreateApplicationBuilder(args);

// --- 1. CONFIGURATION RETRIEVAL ---
string domain = builder.Configuration["FRESHDESK_DOMAIN"] ?? "eiva";
string apiKey = builder.Configuration["FRESHDESK_API_KEY"] ?? "YOUR_KEY";
string weaviateUrl = builder.Configuration["WEAVIATE_URL"] ?? "https://your-cluster.weaviate.network";
string weaviateKey = builder.Configuration["WEAVIATE_API_KEY"] ?? "YOUR_KEY";
string mistralKey = builder.Configuration["MISTRAL_API_KEY"] ?? "YOUR_KEY";

// 💡 FIXED: Explicitly set with correct Pascal casing 'cleanMistralKey' to fix CS0103
string cleanMistralKey = mistralKey.Trim('\"', ' ');
var freshdeskAuthToken = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{apiKey}:X"));

// --- 2. FRESHDESK INTEGRATIONS (TICKETS & DOCUMENTATION) ---
builder.Services.AddHttpClient<IFreshdeskService, FreshdeskService>(client =>
{
    client.BaseAddress = new Uri($"https://{domain}.freshdesk.com/api/v2/");
    client.DefaultRequestHeaders.Accept.Clear();
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", freshdeskAuthToken);
});

builder.Services.AddHttpClient<IDocumentationCrawler, DocumentationCrawler>(client =>
{
    client.BaseAddress = new Uri($"https://{domain}.freshdesk.com/api/v2/");
    client.DefaultRequestHeaders.Accept.Clear();
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", freshdeskAuthToken);
});

// --- 3. WEAVIATE CLUSTER VECTOR REPOSITORIES ---
builder.Services.AddHttpClient<ITicketRepository, TicketRepository>(client =>
{
    client.BaseAddress = new Uri(weaviateUrl);
    client.DefaultRequestHeaders.Add("X-Weaviate-Api-Key", weaviateKey);
});

builder.Services.AddHttpClient<IGraphRepository, GraphRepository>(client =>
{
    client.BaseAddress = new Uri(weaviateUrl);
    client.DefaultRequestHeaders.Add("X-Weaviate-Api-Key", weaviateKey);
});

builder.Services.AddHttpClient<IDocumentationRepository, DocumentationRepository>(client =>
{
    client.BaseAddress = new Uri(weaviateUrl);
    client.DefaultRequestHeaders.Add("X-Weaviate-Api-Key", weaviateKey);
});

builder.Services.AddHttpClient<IKnowledgeRetrievalRepository, KnowledgeRetrievalRepository>(client =>
{
    client.BaseAddress = new Uri(weaviateUrl);
    client.DefaultRequestHeaders.Add("X-Weaviate-Api-Key", weaviateKey);
});

// --- 4. MISTRAL AI EXTRACTOR CONFIGURATION ---
builder.Services.AddHttpClient<IMistralChatService, MistralChatService>(client =>
{
    client.BaseAddress = new Uri("https://api.mistral.ai/");
    client.DefaultRequestHeaders.Clear();
    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {cleanMistralKey}");
});

builder.Services.AddHttpClient<IExtractionEngine, MistralExtractionEngine>(client =>
{
    client.BaseAddress = new Uri("https://api.mistral.ai/");
    client.DefaultRequestHeaders.Clear();
    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {cleanMistralKey}");
});

// --- 5. CORE SYSTEM ENGINE ORCHESTRATION ---
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(AskEiva.Application.Tickets.Commands.IngestTicketsCommand).Assembly));
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
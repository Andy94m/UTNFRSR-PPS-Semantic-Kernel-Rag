// ============================================================================
// ChatRAG - Application Entry Point
//
// Configura el pipeline completo de inyección de dependencias y middleware:
//
// ── Logging ─────────────────────────────────────────────────────────────────
// Serilog con dos sinks:
//   - Console: salida estándar para desarrollo
//   - File: logs rotativos diarios en logs/chatrag-*.log (14 días de retención)
// Se activa con builder.Host.UseSerilog() que reemplaza el ILoggerFactory
// de ASP.NET Core para que todos los ILogger<T> del app usen Serilog.
//
// ── Blazor Server ───────────────────────────────────────────────────────────
// AddRazorPages() + AddServerSideBlazor(): hostea la UI como Blazor Server
// con SignalR para comunicación en tiempo real.
// Mapeo: MapBlazorHub() + MapFallbackToPage("/_Host").
//
// ── Semantic Kernel (Chat) ──────────────────────────────────────────────────
// Registra Kernel como Singleton.
// Configuración leída de appsettings.json → sección "Ollama":
//   - Uri: "http://localhost:11434" (default)
//   - ChatModel: "deepseek-r1" (default)
// Internamente, OllamaChatCompletion usa OllamaSharp → POST /api/chat.
// El Kernel se inyecta en ChatService para generar respuestas.
//
// ── Embedding Service ────────────────────────────────────────────────────────
// Registra IEmbeddingService → EmbeddingService via IHttpClientFactory
// (AddHttpClient). El HttpClient se configura con:
//   - BaseAddress = Ollama:Uri
//   - Timeout = 60 segundos
// Llama a POST /api/embed con modelo "all-minilm" para generar vectores densos
// de 384 dimensiones. Usado por RagService en indexación y consultas.
//
// ── Elasticsearch (Vector Store) ─────────────────────────────────────────────
// IElasticsearchService → ElasticsearchService como Singleton.
// Conexión a Elasticsearch:Uri (default "http://localhost:9200").
// Índice "text_chunks" con campo dense_vector (384 dims, similitud coseno).
// Provee: creación de índice, indexado de chunks, búsqueda KNN.
//
// ── Servicios de Aplicación ──────────────────────────────────────────────────
// Todos Singleton (estado en memoria para sesión única):
//   - ITextChunkerService → TextChunkerService: divide texto con SK TextChunker
//   - IRagService → RagService: orquesta chunking → embedding → ES (indexación)
//     y query → embedding → KNN → contexto (recuperación)
//   - IChatService → ChatService: recibe preguntas, llama a RAG, envía a SK
//
// ── Middleware ───────────────────────────────────────────────────────────────
// Development: errores detallados vía appsettings.Development.json
// Producción: ExceptionHandler /Error + HSTS + HTTPS redirection
// StaticFiles: wwwroot/ (CSS, JS, favicon)
// Routing: Blazor Server hub + fallback a _Host
// ============================================================================
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Ollama;
using Serilog;
using ChatRAG.Services;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File(
        path: "logs/chatrag-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog();

// --- Blazor ---
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

// --- Semantic Kernel (Ollama Chat) ---
builder.Services.AddSingleton<Kernel>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>().GetSection("Ollama");
    var uri = config["Uri"] ?? "http://localhost:11434";
    var chatModel = config["ChatModel"] ?? "deepseek-r1";

    var httpClient = new HttpClient
    {
        BaseAddress = new Uri(uri),
        Timeout = TimeSpan.FromMinutes(5)
    };
    var kernelBuilder = Kernel.CreateBuilder();
    kernelBuilder.AddOllamaChatCompletion(chatModel, httpClient);
    return kernelBuilder.Build();
});

// --- Embedding Service via Ollama HTTP ---
builder.Services.AddHttpClient<IEmbeddingService, EmbeddingService>(client =>
{
    var uri = builder.Configuration.GetSection("Ollama")["Uri"] ?? "http://localhost:11434";
    client.BaseAddress = new Uri(uri);
    client.Timeout = TimeSpan.FromMinutes(5);
});

// --- Elasticsearch (vector store) ---
builder.Services.AddSingleton<IElasticsearchService>(sp =>
{
    var uri = builder.Configuration.GetSection("Elasticsearch")["Uri"] ?? "http://localhost:9200";
    return new ElasticsearchService(uri);
});

// --- Servicios de la aplicación ---
builder.Services.AddSingleton<ITextChunkerService, TextChunkerService>();
builder.Services.AddSingleton<IRagService, RagService>();
builder.Services.AddSingleton<IChatService, ChatService>();
builder.Services.AddSingleton<IDocumentParserService, DocumentParserService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();

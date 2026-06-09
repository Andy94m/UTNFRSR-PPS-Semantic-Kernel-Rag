using System.Net.Http.Json;

namespace ChatRAG.Services;

/// <summary>
/// Genera vectores densos (embeddings) llamando a la API REST de Ollama.
/// Usa el modelo all-minilm a través del endpoint /api/embed.
/// </summary>
/// <remarks>
/// Implementa <see cref="IEmbeddingService"/>.
/// Registrado via AddHttpClient en Program.cs — el HttpClient es creado y
/// gestionado por IHttpClientFactory (pooling de conexiones, timeout, etc.).
///
/// ── Configuración del HttpClient ────────────────────────────────────────────
///   BaseAddress = Ollama:Uri (leído de appsettings.json, default http://localhost:11434)
///   Timeout = 60 segundos
///
/// ── GenerateEmbeddingAsync(string text) : Task&lt;float[]&gt; ──────────────────
/// Parámetro: text — texto a convertir en embedding (puede ser un chunk o una consulta)
/// Retorna: float[] — vector de 384 dimensiones (dimensionalidad de all-minilm)
///
/// Llamada HTTP:
///   POST {BaseAddress}/api/embed
///   Body (JSON): { "model": "all-minilm", "input": text }
///
///   Nota: El endpoint /api/embed (sin 's') es la API moderna de Ollama (>= 0.3.0).
///   La API anterior (/api/embeddings con campo "prompt") fue deprecada.
///   El campo "input" acepta string o string[]; acá se envía string simple.
///
///   Respuesta esperada (JSON):
///   {
///     "model": "all-minilm",
///     "embeddings": [ [0.123, 0.456, ...] ]  // float[][] con un vector por input
///   }
///
///   Se extrae result.Embeddings[0] (FirstOrDefault) como float[].
///
/// ── Errores posibles ────────────────────────────────────────────────────────
///   - Si Ollama no está corriendo: HttpRequestException (conexión rechazada)
///   - Si el modelo all-minilm no está descargado: HttpRequestException 404
///   - Si la respuesta no contiene embeddings: InvalidOperationException
///
/// ── Uso en el sistema ───────────────────────────────────────────────────────
///   Llamado desde dos lugares en RagService:
///   1. IndexTextAsync → por cada chunk de texto (N llamadas por archivo)
///   2. RetrieveContextAsync → una llamada para la consulta del usuario
/// </remarks>
public class EmbeddingService : IEmbeddingService
{
    private readonly HttpClient _httpClient;
    private const string Model = "all-minilm";

    public EmbeddingService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/embed", new
        {
            model = Model,
            input = text
        });

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OllamaEmbedResponse>();
        return result?.Embeddings?.FirstOrDefault()
            ?? throw new InvalidOperationException("No se pudo generar el embedding.");
    }

    private record OllamaEmbedResponse(float[][] Embeddings);
}

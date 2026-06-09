namespace ChatRAG.Services;

/// <summary>
/// Contrato para la generación de vectores densos (embeddings) a partir de texto.
/// Usa el modelo all-minilm de Ollama vía HTTP REST.
/// </summary>
/// <remarks>
/// Implementación concreta: <see cref="EmbeddingService"/>.
/// Registrado via AddHttpClient en Program.cs — el HttpClient se configura con:
///   - BaseAddress = Ollama:Uri (default http://localhost:11434)
///   - Timeout = 60 segundos
///
/// ── GenerateEmbeddingAsync(string text) ────────────────────────────────────
/// Envía POST /api/embed a Ollama con body { model: "all-minilm", input: text }.
/// Retorna un float[] de 384 dimensiones (dimensionalidad de all-minilm).
///
/// El embedding se usa en dos lugares:
///   1. Indexación: <see cref="IRagService.IndexTextAsync"/> → cada chunk → embedding
///   2. Consulta: <see cref="IRagService.RetrieveContextAsync"/> → la pregunta → embedding
///      para búsqueda KNN en Elasticsearch
///
/// Respuesta esperada de Ollama: { "embeddings": [[float, ...]] }
/// Se extrae el primer (y único) embedding del array de arrays.
///
/// Errores:
/// - Si Ollama devuelve status != 200 → HttpRequestException
/// - Si el embedding es null → InvalidOperationException
/// </remarks>
public interface IEmbeddingService
{
    Task<float[]> GenerateEmbeddingAsync(string text);
}

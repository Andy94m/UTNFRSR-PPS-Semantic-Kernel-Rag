using ChatRAG.Models;

namespace ChatRAG.Services;

/// <summary>
/// Contrato para operaciones de almacenamiento y búsqueda de vectores densos
/// en Elasticsearch. Trabaja con el índice "text_chunks" que contiene campos
/// text, keyword y dense_vector (384 dimensiones, similitud coseno).
/// </summary>
/// <remarks>
/// Implementación concreta: <see cref="ElasticsearchService"/> — registrado como Singleton en Program.cs.
/// Conexión configurada desde appsettings.json → sección "Elasticsearch" → Uri.
///
/// ── CreateIndexIfNotExistsAsync(int dimensions = 384) ─────────────────────
/// Verifica si el índice "text_chunks" existe. Si no, lo crea con mappings:
///   - Content (text): texto del fragmento
///   - SourceFile (keyword): nombre del archivo original
///   - Embedding (dense_vector, 384 dims, cosine): vector de embedding
/// Usa la propiedad Id de TextChunk como _id del documento ES.
///
/// ── IndexChunkAsync(TextChunk chunk) ──────────────────────────────────────
/// Indexa un TextChunk en ES. Serialización automática via Elastic.Clients.Elasticsearch.
/// El Id del documento ES se mapea desde chunk.Id (GUID).
///
/// ── SearchAsync(float[] queryEmbedding, int topK = 5) ─────────────────────
/// Búsqueda KNN (k-Nearest Neighbors) por similitud coseno:
///   - k = topK (cantidad de resultados)
///   - NumCandidates = topK * 10 (candidatos a evaluar, más = mejor recall)
///   - SourceIncludes: solo "content", "sourceFile", "id"
/// Retorna List&lt;TextChunk&gt; con los fragmentos más similares.
/// Si la respuesta no es válida, retorna lista vacía.
/// </remarks>
public interface IElasticsearchService
{
    Task CreateIndexIfNotExistsAsync(int dimensions = 384);
    Task IndexChunkAsync(TextChunk chunk);
    Task<List<TextChunk>> SearchAsync(float[] queryEmbedding, int topK = 5);
}

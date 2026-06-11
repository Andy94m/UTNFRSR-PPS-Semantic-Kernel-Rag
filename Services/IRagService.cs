namespace ChatRAG.Services;

/// <summary>
/// Contrato para el orquestador del pipeline RAG (Retrieval-Augmented Generation).
/// Conecta el chunking de texto, la generación de embeddings y el almacenamiento/búsqueda
/// vectorial en Elasticsearch en un solo flujo.
/// </summary>
/// <remarks>
/// Implementación concreta: <see cref="RagService"/> — registrado como Singleton en Program.cs.
///
/// ── IndexTextAsync(string fileName, string content) ───────────────────────
/// Pipeline de indexación de documentos:
/// 1. Crea el índice "text_chunks" en ES si no existe (384 dims, coseno)
/// 2. Divide el contenido en fragmentos vía <see cref="ITextChunkerService"/>
/// 3. Por cada fragmento:
///    a. Genera embedding vía <see cref="IEmbeddingService"/> (POST /api/embed)
///    b. Crea un <see cref="TextChunk"/> con contenido + embedding
///    c. Indexa en ES vía <see cref="IElasticsearchService"/>
/// 4. Loggea cantidad de fragmentos indexados
///
/// ── RetrieveContextAsync(string query) ────────────────────────────────────
/// Pipeline de recuperación de contexto:
/// 1. Genera embedding de la consulta (mismo IEmbeddingService)
/// 2. Busca los topK=5 fragmentos más similares vía KNN en ES
/// 3. Si no hay resultados, retorna mensaje indicando que no hay documentos
/// 4. Formatea cada chunk como "[SourceFile]\nContent"
/// 5. Concatena con separador "---" y retorna el contexto como string
///    (este string se inyecta en el system prompt del chat)
///
/// Manejo de errores:
/// - Cualquier excepción se captura, loguea con ILogger y se relanza.
/// </remarks>
public interface IRagService
{
    Task IndexTextAsync(string fileName, string content);
    Task<string> RetrieveContextAsync(string query);
    Task<bool> HasDocumentsAsync();
}

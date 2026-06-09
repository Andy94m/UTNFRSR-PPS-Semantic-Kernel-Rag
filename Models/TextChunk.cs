namespace ChatRAG.Models;

/// <summary>
/// Modelo de datos para un fragmento de texto con su vector de embedding.
/// Se mapea automáticamente al índice "text_chunks" en Elasticsearch.
/// </summary>
/// <remarks>
/// Propiedades:
///   Id (string) — Identificador único del fragmento.
///     Generado como Guid.NewGuid().ToString() en el constructor.
///     Se mapea como _id del documento en ES (via DefaultMappingFor.IdProperty).
///     No se incluye en SourceIncludes de las búsquedas KNN, pero ES lo devuelve igual.
///
///   Content (string) — Texto del fragmento (el contenido real del documento).
///     Mapeado como "text" en ES → analizado, permite búsqueda full-text si se desea.
///     Viene del resultado de TextChunkerService.ChunkText().
///
///   SourceFile (string) — Nombre del archivo original del que se extrajo el fragmento.
///     Mapeado como "keyword" en ES → exacto, permite filtrado y agregaciones.
///     Se usa en RetrieveContextAsync para mostrar la fuente en el contexto: "[archivo]\ncontenido".
///
///   Embedding (float[]) — Vector denso de 384 dimensiones (all-minilm).
///     Mapeado como "dense_vector" en ES con:
///       dims: 384, index: true, similarity: cosine
///     Generado por EmbeddingService.GenerateEmbeddingAsync() vía POST /api/embed.
///     No se incluye en SourceIncludes de las búsquedas para ahorrar ancho de banda.
///     Default: [] (array vacío)
///
/// ── Ciclo de vida ────────────────────────────────────────────────────────────
/// Creación: RagService.IndexTextAsync()
///   foreach (var chunkText in chunks)
///   {
///       var embedding = await _embedder.GenerateEmbeddingAsync(chunkText);
///       var chunk = new TextChunk
///       {
///           Content = chunkText,
///           SourceFile = fileName,
///           Embedding = embedding
///       };
///       await _elasticsearch.IndexChunkAsync(chunk);
///   }
///
/// Consulta: ElasticsearchService.SearchAsync()
///   → Retorna List&lt;TextChunk&gt; deserializado automáticamente por ES client
///   → Solo con campos "content", "sourceFile", "id" (embedding se omite)
///
/// ── Persistencia ─────────────────────────────────────────────────────────────
/// Los TextChunk se persisten en Elasticsearch, no en memoria. Si se reinicia
/// la app, los documentos subidos previamente siguen disponibles en ES.
/// Para eliminarlos habría que borrar el índice "text_chunks" manualmente.
/// </remarks>
public class TextChunk
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Content { get; set; } = string.Empty;
    public string SourceFile { get; set; } = string.Empty;
    public float[] Embedding { get; set; } = [];
}

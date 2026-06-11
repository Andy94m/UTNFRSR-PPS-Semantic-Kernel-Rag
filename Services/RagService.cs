using ChatRAG.Models;

namespace ChatRAG.Services;

/// <summary>
/// Orquesta el pipeline RAG completo conectando chunking, embeddings, y Elasticsearch.
/// Provee dos flujos principales: indexación de documentos y recuperación de contexto.
/// </summary>
/// <remarks>
/// Implementa <see cref="IRagService"/> — registrado como Singleton en Program.cs.
///
/// ── Dependencias inyectadas por constructor ────────────────────────────────
/// Todas Singleton, resueltas por DI:
///
///   ITextChunkerService _chunker
///     └─ Fragmenta el texto en chunks de ~500 tokens con 50 de solapamiento
///     └─ Usa SK TextChunker (SplitPlainTextLines → SplitPlainTextParagraphs)
///
///   IEmbeddingService _embedder
///     └─ Genera vectores densos de 384 dimensiones vía POST /api/embed
///     └─ Modelo all-minilm en Ollama
///     └─ HttpClient configurado con timeout de 60s
///
///   IElasticsearchService _elasticsearch
///     └─ Conexión a ES en http://localhost:9200 (configurable)
///     └─ Índice "text_chunks" con campo dense_vector (384 dims, coseno)
///
///   ILogger&lt;RagService&gt; _logger
///     └─ Serilog con sink a consola y archivo logs/chatrag-*.log
///
/// ── IndexTextAsync(string fileName, string content) ────────────────────────
/// Parámetros:
///   fileName — nombre del archivo original (se guarda en SourceFile de cada chunk)
///   content — texto completo del documento a indexar
///
/// Pipeline:
/// 1. _elasticsearch.CreateIndexIfNotExistsAsync()
///    → Verifica si el índice "text_chunks" existe; si no, lo crea con mappings
///      (text, keyword, dense_vector). Solo crea una vez.
///
/// 2. _chunker.ChunkText(content) → List&lt;string&gt; fragments
///    → Divide content en fragmentos de ~500 tokens con ~50 de solapamiento
///
/// 3. Por cada fragmento en fragments (secuencial, uno por uno):
///    a. _embedder.GenerateEmbeddingAsync(chunkText) → float[] embedding (384 dims)
///       → POST /api/embed a Ollama (modelo all-minilm, campo "input")
///       → Espera la respuesta (latencia de red + cómputo en Ollama)
///    b. Crea TextChunk { Id=GUID, Content, SourceFile=fileName, Embedding }
///    c. _elasticsearch.IndexChunkAsync(chunk)
///       → Indexa el documento en ES, ID = chunk.Id
///
/// 4. Loggea "Indexados N fragmentos de 'fileName' en Elasticsearch."
///
/// ── RetrieveContextAsync(string query) : Task&lt;string&gt; ──────────────────
/// Parámetro: query — texto de la pregunta del usuario
/// Retorna: string — contexto formateado para inyectar en el system prompt
///
/// Pipeline:
/// 1. _embedder.GenerateEmbeddingAsync(query) → float[] queryEmbedding (384 dims)
///    → Mismo proceso que en indexación, pero sobre la consulta del usuario
///
/// 2. _elasticsearch.SearchAsync(queryEmbedding, topK: 5) → List&lt;TextChunk&gt;
///    → Búsqueda KNN: encuentra los 5 chunks más similares por coseno
///    → NumCandidates = 50 (topK * 10) para mejor recall
///    → SourceIncludes: solo "content", "sourceFile", "id" (evita traer embedding)
///
/// 3. Si similarChunks.Count == 0:
///    → Retorna "No hay documentos indexados para proporcionar contexto."
///    → El LLM recibirá este mensaje y responderá que no encontró información
///
/// 4. Formatea cada chunk como "[SourceFile]\nContent"
///    → SourceFile es el nombre del archivo original
///    → Content es el texto del fragmento (sin el embedding)
///
/// 5. Concatena con separador "\n\n---\n\n" y retorna el string
///    → Este string se inyecta directamente en el system prompt del ChatHistory
///      en ChatService.AskAsync()
///
/// ── Manejo de errores ───────────────────────────────────────────────────────
/// Ambos métodos envuelven su lógica en try-catch. Cualquier excepción se
/// loggea con _logger.LogError y se relanza para que el llamador (generalmente
/// ChatService o Upload.razor) la maneje.
/// </remarks>
public class RagService : IRagService
{
    private readonly ITextChunkerService _chunker;
    private readonly IEmbeddingService _embedder;
    private readonly IElasticsearchService _elasticsearch;
    private readonly ILogger<RagService> _logger;

    public RagService(
        ITextChunkerService chunker,
        IEmbeddingService embedder,
        IElasticsearchService elasticsearch,
        ILogger<RagService> logger)
    {
        _chunker = chunker;
        _embedder = embedder;
        _elasticsearch = elasticsearch;
        _logger = logger;
    }

    public async Task IndexTextAsync(string fileName, string content)
    {
        try
        {
            await _elasticsearch.CreateIndexIfNotExistsAsync();

            var chunks = _chunker.ChunkText(content);
            _logger.LogInformation("Dividido '{FileName}' en {Count} fragmentos.", fileName, chunks.Count);

            foreach (var chunkText in chunks)
            {
                var embedding = await _embedder.GenerateEmbeddingAsync(chunkText);
                var chunk = new TextChunk
                {
                    Content = chunkText,
                    SourceFile = fileName,
                    Embedding = embedding
                };
                await _elasticsearch.IndexChunkAsync(chunk);
            }

            _logger.LogInformation("Indexados {Count} fragmentos de '{FileName}' en Elasticsearch.", chunks.Count, fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al indexar '{FileName}'", fileName);
            throw;
        }
    }

    public async Task<bool> HasDocumentsAsync()
    {
        try
        {
            var count = await _elasticsearch.GetDocumentCountAsync();
            return count > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al verificar si existen documentos.");
            return false;
        }
    }

    public async Task<string> RetrieveContextAsync(string query)
    {
        try
        {
            var queryEmbedding = await _embedder.GenerateEmbeddingAsync(query);
            var similarChunks = await _elasticsearch.SearchAsync(queryEmbedding, topK: 5);

            if (similarChunks.Count == 0)
                return "No hay documentos indexados para proporcionar contexto.";

            var contextParts = similarChunks
                .Select(c => $"[{c.SourceFile}]\n{c.Content}")
                .ToList();

            return string.Join("\n\n---\n\n", contextParts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al recuperar contexto para: {Query}", query);
            throw;
        }
    }
}

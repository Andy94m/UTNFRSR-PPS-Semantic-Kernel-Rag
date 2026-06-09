using ChatRAG.Models;
using Elastic.Clients.Elasticsearch;

namespace ChatRAG.Services;

/// <summary>
/// Implementa el almacenamiento y búsqueda de vectores densos en Elasticsearch.
/// Gestiona el índice "text_chunks" con mappings de texto, keyword y dense_vector
/// (384 dimensiones, similitud coseno), y provee búsqueda KNN.
/// </summary>
/// <remarks>
/// Implementa <see cref="IElasticsearchService"/> — registrado como Singleton en Program.cs.
/// Usa el cliente oficial Elastic.Clients.Elasticsearch (v8.17.0).
///
/// ── Configuración ───────────────────────────────────────────────────────────
///   Uri configurable desde appsettings.json → sección "Elasticsearch" → Uri
///   Default: http://localhost:9200
///   Docker Compose incluido: docker compose up desde la raíz del proyecto
///     (imagen docker.elastic.co/elasticsearch/elasticsearch:8.17.0, single-node,
///      xpack.security.enabled=false, puerto 9200)
///
/// ── Constructor(string uri) ─────────────────────────────────────────────────
///   Recibe la URI de ES como parámetro (string, no inyectado por DI — se pasa
///   manualmente desde el factory method en Program.cs).
///   Crea ElasticsearchClientSettings con:
///     - DefaultMappingFor&lt;TextChunk&gt;: mapea al índice "text_chunks"
///     - IdProperty: usa t.Id como _id del documento
///
/// ── CreateIndexIfNotExistsAsync(int dimensions = 384) ──────────────────────
///   1. Verifica existencia del índice "text_chunks" con Indices.ExistsAsync
///   2. Si existe: retorna sin cambios (no actualiza mappings)
///   3. Si no existe: crea el índice con:
///      - Content (text): texto plano del fragmento
///      - SourceFile (keyword): nombre del archivo (filtrable, aggregable)
///      - Embedding (dense_vector):
///          dims: 384 (dimensionalidad de all-minilm)
///          index: true (habilita búsqueda por proximidad)
///          similarity: cosine (medida de similitud coseno)
///
/// ── IndexChunkAsync(TextChunk chunk) ────────────────────────────────────────
///   Indexa un TextChunk en ES. Serialización automática del objeto a JSON
///   por el cliente. El _id del documento ES es chunk.Id (GUID).
///   Si la respuesta no es válida, lanza InvalidOperationException con el error
///   devuelto por ES.
///
/// ── SearchAsync(float[] queryEmbedding, int topK = 5) : List&lt;TextChunk&gt; ──
///   Parámetros:
///     queryEmbedding — float[] de 384 dimensiones (generado por EmbeddingService)
///     topK — cantidad de resultados a retornar (default: 5)
///
///   Búsqueda KNN (k-Nearest Neighbors):
///     Field: "embedding" (campo dense_vector)
///     QueryVector: queryEmbedding
///     k: topK (5 resultados finales)
///     NumCandidates: topK * 10 = 50 (cantidad de nodos a evaluar)
///       → Mayor NumCandidates = mejor recall pero más latencia
///
///   SourceIncludes: ["content", "sourceFile", "id"]
///     → Omite el campo embedding en la respuesta (~1.5KB de datos por doc)
///     → Solo devuelve los campos necesarios para armar el contexto
///
///   Si la respuesta no es válida (ES caído, índice no existe, etc.):
///     → Retorna lista vacía (no lanza excepción)
///   Si es válida:
///     → Retorna response.Documents.ToList() deserializado automáticamente
///
/// ── Dependencias externas ───────────────────────────────────────────────────
///   Elastic.Clients.Elasticsearch: paquete NuGet v8.17.0
///   Elasticsearch server: Docker image docker.elastic.co/elasticsearch/elasticsearch:8.17.0
/// </remarks>
public class ElasticsearchService : IElasticsearchService
{
    private readonly ElasticsearchClient _client;
    private const string IndexName = "text_chunks";

    public ElasticsearchService(string uri)
    {
        var settings = new ElasticsearchClientSettings(new Uri(uri))
            .DefaultMappingFor<TextChunk>(m => m
                .IndexName(IndexName)
                .IdProperty(t => t.Id)
            );
        _client = new ElasticsearchClient(settings);
    }

    public async Task CreateIndexIfNotExistsAsync(int dimensions = 384)
    {
        var exists = await _client.Indices.ExistsAsync(IndexName);
        if (exists.Exists) return;

        var response = await _client.Indices.CreateAsync<TextChunk>(IndexName, c => c
            .Mappings(m => m
                .Properties(p => p
                    .Text(t => t.Content)
                    .Keyword(t => t.SourceFile)
                    .DenseVector(t => t.Embedding, dv => dv
                        .Dims(dimensions)
                        .Index(true)
                        .Similarity(Elastic.Clients.Elasticsearch.Mapping.DenseVectorSimilarity.Cosine)
                    )
                )
            )
        );

        if (!response.IsValidResponse)
            throw new InvalidOperationException($"Error al crear el índice: {response.ElasticsearchServerError?.Error}");
    }

    public async Task IndexChunkAsync(TextChunk chunk)
    {
        var response = await _client.IndexAsync(chunk);
        if (!response.IsValidResponse)
            throw new InvalidOperationException($"Error al indexar el chunk: {response.ElasticsearchServerError?.Error}");
    }

    public async Task<List<TextChunk>> SearchAsync(float[] queryEmbedding, int topK = 5)
    {
        var response = await _client.SearchAsync<TextChunk>(new SearchRequest(IndexName)
        {
            Knn = new List<KnnSearch>
            {
                new KnnSearch
                {
                    Field = new Field("embedding"),
                    QueryVector = queryEmbedding,
                    k = topK,
                    NumCandidates = topK * 10
                }
            },
            SourceIncludes = new[] { "content", "sourceFile", "id" }
        });

        if (!response.IsValidResponse)
            return [];

        return response.Documents.ToList();
    }
}

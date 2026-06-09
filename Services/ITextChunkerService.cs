namespace ChatRAG.Services;

/// <summary>
/// Contrato para el servicio de fragmentación de texto.
/// Divide documentos largos en fragmentos más pequeños (chunks) para facilitar
/// el embedding y la búsqueda semántica.
/// </summary>
/// <remarks>
/// Implementación concreta: <see cref="TextChunkerService"/> — registrado como Singleton.
///
/// ── ChunkText(string text, int maxTokensPerChunk = 500, int overlapTokens = 50) ──
/// Usa <see cref="Microsoft.SemanticKernel.Text.TextChunker"/> de Semantic Kernel
/// (experimental, requiere #pragma warning disable SKEXP0050):
///
/// 1. SplitPlainTextLines: divide el texto en líneas de ~166 tokens cada una
///    (maxTokensPerChunk / 3). Este paso intermedio preserva fronteras de palabras.
///
/// 2. SplitPlainTextParagraphs: agrupa las líneas en párrafos de hasta
///    maxTokensPerChunk (500) tokens con overlapTokens (50) de solapamiento.
///    El solapamiento asegura que ningún contexto se pierda entre fragmentos.
///
/// Retorna List&lt;string&gt; — cada elemento es un fragmento listo para embedear.
/// Usado exclusivamente por <see cref="IRagService.IndexTextAsync"/>.
/// </remarks>
public interface ITextChunkerService
{
    List<string> ChunkText(string text, int maxTokensPerChunk = 500, int overlapTokens = 50);
}

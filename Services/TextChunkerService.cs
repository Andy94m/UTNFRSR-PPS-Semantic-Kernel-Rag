using Microsoft.SemanticKernel.Text;

namespace ChatRAG.Services;

/// <summary>
/// Divide textos largos en fragmentos (chunks) con solapamiento usando el
/// TextChunker experimental de Semantic Kernel.
/// </summary>
/// <remarks>
/// Implementa <see cref="ITextChunkerService"/> — registrado como Singleton en Program.cs.
///
/// ── ChunkText(string text, int maxTokensPerChunk = 500, int overlapTokens = 50) ──
/// Parámetros:
///   text — contenido completo del documento a fragmentar
///   maxTokensPerChunk — tamaño máximo de cada fragmento en tokens (default: 500)
///   overlapTokens — tokens de solapamiento entre fragmentos consecutivos (default: 50)
///     → El solapamiento evita que contextos relevantes queden divididos entre chunks
/// Retorna: List&lt;string&gt; — lista de fragmentos de texto
///
/// Pipeline interno (SK TextChunker):
///   Paso 1: SplitPlainTextLines(text, maxTokensPerLine: ~166)
///     → Divide el texto en líneas de ~166 tokens (maxTokensPerChunk / 3)
///     → Preserva fronteras de palabras (no corta palabras a la mitad)
///
///   Paso 2: SplitPlainTextParagraphs(lines, maxTokensPerParagraph: 500, overlap: 50)
///     → Agrupa líneas en párrafos de hasta 500 tokens
///     → Cada párrafo solapa ~50 tokens con el anterior
///     → Los párrafos resultantes son los "chunks" finales
///
/// ⚠️ Advertencia: TextChunker es experimental en SK (SKEXP0050).
///    Se suprime con #pragma warning disable SKEXP0050.
///    En versiones futuras de SK podría cambiar o moverse a otro namespace.
///
/// ── Uso en el sistema ────────────────────────────────────────────────────────
/// Llamado exclusivamente desde <see cref="RagService.IndexTextAsync"/>:
///   var chunks = _chunker.ChunkText(content);
///   foreach (var chunk in chunks) { embed → index }
///
/// No se usa en el flujo de consulta (RetrieveContextAsync) porque la query
/// del usuario se embediza completa, no fragmentada.
/// </remarks>
#pragma warning disable SKEXP0050
public class TextChunkerService : ITextChunkerService
{
    public List<string> ChunkText(string text, int maxTokensPerChunk = 500, int overlapTokens = 50)
    {
        var lines = TextChunker.SplitPlainTextLines(text, maxTokensPerLine: maxTokensPerChunk / 3);
        var paragraphs = TextChunker.SplitPlainTextParagraphs(
            lines,
            maxTokensPerParagraph: maxTokensPerChunk,
            overlapTokens: overlapTokens
        );
        return paragraphs.ToList();
    }
}
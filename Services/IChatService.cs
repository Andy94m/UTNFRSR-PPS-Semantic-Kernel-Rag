using ChatRAG.Models;

namespace ChatRAG.Services;

/// <summary>
/// Contrato del servicio que gestiona la conversación del chat.
/// Mantiene el historial de mensajes en memoria y coordina la generación de
/// respuestas usando Semantic Kernel + Ollama + contexto RAG.
/// </summary>
/// <remarks>
/// Implementación concreta: <see cref="ChatService"/> — registrado como Singleton en Program.cs.
///
/// ── Messages ──────────────────────────────────────────────────────────────
/// IReadOnlyList&lt;ChatMessage&gt;: historial cronológico de toda la conversación
/// desde que inició la app. Se usa en Chat.razor para renderizar el chat UI.
///
/// ── AskAsync(string question) ─────────────────────────────────────────────
/// 1. Agrega el mensaje del usuario al historial local (_messages)
/// 2. Recupera contexto vía <see cref="IRagService.RetrieveContextAsync"/>:
///    a. Genera embedding de la query (POST /api/embed → all-minilm)
///    b. Búsqueda KNN en Elasticsearch (índice "text_chunks", topK=5)
///    c. Formatea chunks como "[archivo]\ncontenido"
/// 3. Construye ChatHistory con:
///    - System prompt que instruye responder SOLO con el contexto
///    - Historial de mensajes previos (sin incluir el actual)
///    - Pregunta del usuario actual
/// 4. Envía a IChatCompletionService (SK → OllamaSharp → POST /api/chat)
/// 5. Guarda respuesta en historial y la retorna como string
///
/// ── ClearHistory() ────────────────────────────────────────────────────────
/// Vacía la lista _messages. No afecta a Elasticsearch ni al contexto.
///
/// Manejo de errores:
/// - Cualquier excepción es capturada, logueada con ILogger y relanzada.
/// - La UI (Chat.razor) captura el error y lo muestra al usuario.
/// </remarks>
public interface IChatService
{
    IReadOnlyList<ChatMessage> Messages { get; }
    Task<string> AskAsync(string question);
    void ClearHistory();
    Task<bool> HasDocumentsAsync();
    void AddAssistantMessage(string content);
}

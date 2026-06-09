namespace ChatRAG.Models;

/// <summary>
/// Modelo que representa un mensaje individual dentro de la conversación del chat.
/// Se usa para mantener el historial de la conversación en memoria y para
/// renderizar la UI del chat en Chat.razor.
/// </summary>
/// <remarks>
/// Propiedades:
///   Role (string) — "user" o "assistant". Determina la alineación y estilo
///     del mensaje en la UI (derecha para user, izquierda para assistant).
///     Default: "assistant"
///
///   Content (string) — texto del mensaje. Puede contener saltos de línea.
///     Para el usuario: la pregunta exacta que escribió.
///     Para el asistente: la respuesta generada por el LLM.
///     Default: string.Empty
///
///   Timestamp (DateTime) — momento UTC en que se creó el mensaje.
///     Se asigna automáticamente en el constructor o al crear la instancia.
///     Default: DateTime.UtcNow
///     Uso: podría usarse para ordenar o mostrar hora, aunque actualmente
///     el orden está garantizado por la posición en la List&lt;ChatMessage&gt;.
///
/// ── Ciclo de vida ────────────────────────────────────────────────────────────
/// Creado en <see cref="ChatService.AskAsync"/>:
///   1. Mensaje de usuario: new ChatMessage { Role = "user", Content = question }
///      → Al inicio de AskAsync, antes de procesar
///   2. Mensaje de asistente: new ChatMessage { Role = "assistant", Content = answer }
///      → Al final de AskAsync, después de recibir respuesta del LLM
///
/// Leído desde Chat.razor:
///   @foreach (var message in ChatService.Messages)
///     → Renderiza cada mensaje con clase CSS "message user" o "message assistant"
///
/// ── Serialización ────────────────────────────────────────────────────────────
/// No se serializa a disco ni a BD. Solo vive en memoria mientras la app corre.
/// Si se reinicia la app, el historial se pierde.
/// </remarks>
public class ChatMessage
{
    public string Role { get; set; } = "assistant";
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

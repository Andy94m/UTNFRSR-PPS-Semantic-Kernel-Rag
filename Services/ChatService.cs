using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using ChatRAG.Models;

namespace ChatRAG.Services;

/// <summary>
/// Gestiona la conversación del chat: recibe preguntas del usuario, recupera
/// contexto relevante vía RAG y genera respuestas usando Semantic Kernel + Ollama.
/// </summary>
/// <remarks>
/// Implementa <see cref="IChatService"/> — registrado como Singleton en Program.cs.
///
/// ── Dependencias inyectadas por constructor ────────────────────────────────
/// Todas resueltas automáticamente por el DI container de ASP.NET Core:
///
///   Kernel _kernel (Singleton)
///     └─ Creado en Program.cs con AddOllamaChatCompletion("deepseek-r1", uri)
///     └─ Internamente usa OllamaSharp → POST /api/chat
///     └─ Se obtiene IChatCompletionService via _kernel.GetRequiredService()
///
///   IRagService _ragService (Singleton)
///     └─ Orquesta embedding + búsqueda KNN en Elasticsearch
///     └─ Recupera contexto relevante para cada pregunta
///
///   ILogger&lt;ChatService&gt; _logger
///     └─ Serilog: escribe a consola y archivo logs/chatrag-*.log
///
/// ── Estado interno ──────────────────────────────────────────────────────────
///   _messages: List&lt;ChatMessage&gt; — historial completo de la conversación.
///   Como ChatService es Singleton, el historial persiste mientras la app corra.
///   Se expone como IReadOnlyList&lt;ChatMessage&gt; via propiedad Messages para
///   que Chat.razor pueda bindear la UI sin modificarlo directamente.
///
/// ── AskAsync(string question) : Task&lt;string&gt; ─────────────────────────────
/// Parámetro: question — texto de la pregunta del usuario.
/// Retorna: string — respuesta generada por el LLM.
///
/// Flujo completo:
/// 1. Agrega el mensaje del usuario a _messages (Role="user")
/// 2. Llama a _ragService.RetrieveContextAsync(question):
///    a. IEmbeddingService.GenerateEmbeddingAsync(query) → float[] (384 dims)
///       → POST /api/embed a Ollama (modelo all-minilm)
///    b. IElasticsearchService.SearchAsync(embedding, topK=5)
///       → Búsqueda KNN en índice "text_chunks" (similitud coseno)
///    c. Si no hay resultados: retorna "No hay documentos indexados..."
///    d. Formatea cada chunk como "[SourceFile]\nContent" y concatena con "---"
/// 3. Construye ChatHistory (system prompt + contexto RAG):
///    - Instrucciones: responder SOLO con el contexto proporcionado
///    - Si no hay info suficiente: responder "No encontré información..."
///    - Contexto inyectado: {context} (string de RetrieveContextAsync)
/// 4. Reconstruye historial previo en el ChatHistory:
///    - Itera _messages excluyendo el último (recién agregado)
///    - Por cada mensaje: AddUserMessage o AddAssistantMessage según Role
/// 5. Agrega la pregunta actual como user message
/// 6. Obtiene IChatCompletionService del Kernel (SK → OllamaSharp)
/// 7. Envía el ChatHistory a Ollama: POST /api/chat (bloqueante ~segundos)
/// 8. Extrae response.Content (texto generado)
/// 9. Agrega respuesta a _messages (Role="assistant")
/// 10. Loggea la interacción y retorna la respuesta
///
/// ── ClearHistory() ──────────────────────────────────────────────────────────
/// Vacía _messages. No afecta a los documentos indexados en ES ni al contexto.
/// Se llama desde Chat.razor → botón "Limpiar historial".
///
/// ── Manejo de errores ───────────────────────────────────────────────────────
/// Cualquier excepción durante el flujo (embedding, ES, SK) se captura en el
/// catch(Exception), se loggea con _logger.LogError (stack trace completo)
/// y se relanza para que Chat.razor lo muestre al usuario en statusMessage.
/// </remarks>
public class ChatService : IChatService
{
    private readonly Kernel _kernel;
    private readonly IRagService _ragService;
    private readonly List<ChatMessage> _messages = [];
    private readonly ILogger<ChatService> _logger;

    public ChatService(Kernel kernel, IRagService ragService, ILogger<ChatService> logger)
    {
        _kernel = kernel;
        _ragService = ragService;
        _logger = logger;
    }

    public IReadOnlyList<ChatMessage> Messages => _messages;

    public async Task<string> AskAsync(string question)
    {
        _messages.Add(new ChatMessage { Role = "user", Content = question, Timestamp = DateTime.UtcNow });

        try
        {
            var context = await _ragService.RetrieveContextAsync(question);

            var chat = new ChatHistory($"""
                Eres un asistente de IA que responde preguntas basándose exclusivamente en los documentos proporcionados.
                
                Instrucciones:
                - Responde ÚNICAMENTE usando la información del contexto que se proporciona abajo.
                - Si el contexto no contiene la información suficiente, responde: "No encontré información sobre eso en los documentos cargados."
                - Responde en el mismo idioma de la pregunta.
                - Sé conciso y directo.
                
                Contexto de los documentos:
                {context}
                """);

            foreach (var msg in _messages.Take(_messages.Count - 1))
            {
                if (msg.Role == "user")
                    chat.AddUserMessage(msg.Content);
                else
                    chat.AddAssistantMessage(msg.Content);
            }

            chat.AddUserMessage(question);

            var chatCompletion = _kernel.GetRequiredService<IChatCompletionService>();
            var response = await chatCompletion.GetChatMessageContentAsync(chat);

            var answer = response.Content ?? "Error: no se pudo generar una respuesta.";
            _messages.Add(new ChatMessage { Role = "assistant", Content = answer, Timestamp = DateTime.UtcNow });

            _logger.LogInformation("Pregunta: {Question} → Respuesta generada con {ContextLength} chars de contexto.",
                question, context.Length);

            return answer;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en AskAsync para pregunta: {Question}", question);
            throw;
        }
    }

    public void ClearHistory()
    {
        _messages.Clear();
    }
}

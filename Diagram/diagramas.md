# Diagramas UML — ChatRAG

## Diagrama de Clases

```mermaid
classDiagram
    %% ==================== MODELOS ====================
    class ChatMessage {
        +string Role
        +string Content
        +DateTime Timestamp
    }

    class TextChunk {
        +string Id
        +string Content
        +string SourceFile
        +float[] Embedding
    }

    %% ==================== INTERFACES ====================
    class IChatService {
        <<interface>>
        +IReadOnlyList~ChatMessage~ Messages
        +Task~string~ AskAsync(string question)
        +void ClearHistory()
    }

    class IRagService {
        <<interface>>
        +Task IndexTextAsync(string fileName, string content)
        +Task~string~ RetrieveContextAsync(string query)
    }

    class IEmbeddingService {
        <<interface>>
        +Task~float[]~ GenerateEmbeddingAsync(string text)
    }

    class IElasticsearchService {
        <<interface>>
        +Task CreateIndexIfNotExistsAsync(int dimensions)
        +Task IndexChunkAsync(TextChunk chunk)
        +Task~List~TextChunk~~ SearchAsync(float[] queryEmbedding, int topK)
    }

    class ITextChunkerService {
        <<interface>>
        +List~string~ ChunkText(string text, int maxTokens, int overlap)
    }

    %% ==================== IMPLEMENTACIONES ====================
    class ChatService {
        -Kernel _kernel
        -IRagService _ragService
        -List~ChatMessage~ _messages
        -ILogger _logger
    }

    class RagService {
        -ITextChunkerService _chunker
        -IEmbeddingService _embedder
        -IElasticsearchService _elasticsearch
        -ILogger _logger
    }

    class EmbeddingService {
        -HttpClient _httpClient
        -const string Model = "all-minilm"
        +Task~float[]~ GenerateEmbeddingAsync(string text)
    }

    class ElasticsearchService {
        -ElasticsearchClient _client
        -const string IndexName = "text_chunks"
    }

    class TextChunkerService {
        +List~string~ ChunkText(string text, int maxTokens, int overlap)
    }

    %% ==================== PÁGINAS BLAZOR ====================
    class UploadPage {
        <<razor component>>
        +bool isProcessing
        +string statusMessage
        +bool isError
        +Task OnFileSelected(InputFileChangeEventArgs e)
    }

    class ChatPage {
        <<razor component>>
        +string userInput
        +bool isLoading
        +string statusMessage
        +Task SendMessage()
        +Task HandleKeyDown(KeyboardEventArgs e)
        +void ClearChat()
    }

    %% ==================== PUNTO DE ENTRADA ====================
    class Program {
        +static void Main()
        +WebApplicationBuilder builder
        -registra servicios en DI
        -configura middleware
    }

    %% ==================== EXTERNOS ====================
    class Kernel {
        <<Semantic Kernel>>
    }
    class HttpClient {
        <<.NET>>
    }
    class ElasticsearchClient {
        <<Elastic.Clients.Elasticsearch>>
    }

    %% ==================== RELACIONES ====================
    ChatService ..|> IChatService
    RagService ..|> IRagService
    EmbeddingService ..|> IEmbeddingService
    ElasticsearchService ..|> IElasticsearchService
    TextChunkerService ..|> ITextChunkerService

    ChatService o--> Kernel : usa
    ChatService o--> IRagService : inyectado
    ChatService *--> "lista" ChatMessage : contiene

    RagService o--> ITextChunkerService : inyectado
    RagService o--> IEmbeddingService : inyectado
    RagService o--> IElasticsearchService : inyectado

    EmbeddingService o--> HttpClient : inyectado

    ElasticsearchService o--> ElasticsearchClient : crea
    ElasticsearchService ..> TextChunk : indexa/busca

    TextChunkerService ..> TextChunk : produce fragmentos

    UploadPage o--> IRagService : inyectado
    ChatPage o--> IChatService : inyectado

    Program ..> ChatService : registra (Singleton)
    Program ..> RagService : registra (Singleton)
    Program ..> EmbeddingService : registra (Singleton)
    Program ..> ElasticsearchService : registra (Singleton)
    Program ..> TextChunkerService : registra (Singleton)
    Program ..> Kernel : registra (Singleton)
```

---

## Diagrama de Secuencia — Chat

```mermaid
sequenceDiagram
    actor User as Usuario
    participant ChatUI as Chat.razor
    participant ChatSvc as ChatService
    participant RAG as RagService
    participant Embed as EmbeddingService
    participant ES as ElasticsearchService
    participant SK as Kernel (SK + Ollama)

    User->>ChatUI: escribe pregunta y presiona Enter
    ChatUI->>ChatUI: userInput = "", isLoading = true
    ChatUI->>ChatSvc: AskAsync(question)
    ChatSvc->>ChatSvc: _messages.Add(user msg)
    ChatSvc->>RAG: RetrieveContextAsync(question)
    RAG->>Embed: GenerateEmbeddingAsync(query)
    Embed-->>RAG: float[384] queryVector
    RAG->>ES: SearchAsync(queryVector, topK=5)
    ES-->>RAG: List~TextChunk~ (top 5 similares)
    RAG->>RAG: formatea "[archivo]\ncontenido---..."
    RAG-->>ChatSvc: string context
    ChatSvc->>ChatSvc: construye ChatHistory (system prompt + contexto + historial + pregunta)
    ChatSvc->>SK: GetChatMessageContentAsync(chat)
    SK-->>ChatSvc: string response
    ChatSvc->>ChatSvc: _messages.Add(assistant msg)
    ChatSvc-->>ChatUI: string answer
    ChatUI->>ChatUI: isLoading = false, renderiza mensajes
    ChatUI-->>User: muestra respuesta del asistente
```

---

## Diagrama de Secuencia — Subida de Documento

```mermaid
sequenceDiagram
    actor User as Usuario
    participant UpUI as Upload.razor
    participant RAG as RagService
    participant Chunker as TextChunkerService
    participant Embed as EmbeddingService
    participant ES as ElasticsearchService

    User->>UpUI: selecciona .txt
    UpUI->>UpUI: isProcessing = true
    UpUI->>UpUI: lee archivo (StreamReader)
    UpUI->>RAG: IndexTextAsync(fileName, content)
    RAG->>ES: CreateIndexIfNotExistsAsync()
    ES-->>RAG: ok (ya existe o se creó)
    RAG->>Chunker: ChunkText(content, 500, 50)
    Chunker-->>RAG: List~string~ fragments
    loop por cada fragmento
        RAG->>Embed: GenerateEmbeddingAsync(chunkText)
        Embed-->>RAG: float[384] embedding
        RAG->>RAG: new TextChunk { Content, SourceFile, Embedding }
        RAG->>ES: IndexChunkAsync(chunk)
        ES-->>RAG: ok
    end
    RAG-->>UpUI: completado
    UpUI->>UpUI: isProcessing = false, mensaje éxito
    UpUI-->>User: "indexado correctamente"
```

---

## Diagrama de Paquetes

```mermaid
packages
    package "ChatRAG" {
        package "Models" {
            component ChatMessage
            component TextChunk
        }
        package "Services" {
            component "Interfaces (IChatService, IRagService, IEmbeddingService, IElasticsearchService, ITextChunkerService)"
            component "Implementaciones (ChatService, RagService, EmbeddingService, ElasticsearchService, TextChunkerService)"
        }
        package "Pages" {
            component "Chat.razor"
            component "Upload.razor"
            component "Index.razor"
        }
        component "Program.cs"
    }
    package "Infraestructura externa" {
        component "Ollama (deepseek-r1 + all-minilm)"
        component "Elasticsearch 8.17"
    }
```

---

## Diagrama de Despliegue

```mermaid
graph LR
    subgraph "Máquina Local (Dev)"
        direction TB
        App[ChatRAG .NET 9<br/>Blazor Server]
        Logs[(logs/chatrag-*.log)]
    end

    subgraph "Docker (docker compose)"
        ES[(Elasticsearch 8.17<br/>puerto 9200<br/>volumen: es_data)]
    end

    subgraph "Ollama (localhost:11434)"
        LLM[deepseek-r1<br/>endpoint: /api/chat]
        EmbedModel[all-minilm<br/>endpoint: /api/embed]
    end

    App -->|POST /api/chat| LLM
    App -->|POST /api/embed| EmbedModel
    App -->|HTTP 9200| ES
    App --> Logs
```

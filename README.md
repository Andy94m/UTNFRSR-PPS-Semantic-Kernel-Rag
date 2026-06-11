# ChatRAG

Proyecto final de PPS: La Aplicación **Blazor Server** (.NET 9) que implementa un sistema **RAG (Retrieval-Augmented Generation)** para chatear con documentos de texto, PDF y DOCX usando un LLM local.

## Stack tecnológico

| Componente | Tecnología |
|---|---|
| UI | Blazor Server (SignalR) |
| LLM | DeepSeek-R1 vía Ollama (Semantic Kernel) |
| Embeddings | all-minilm vía Ollama |
| Vector DB | Elasticsearch 8.17 |
| Logging | Serilog |
| Runtime | .NET 9 |

## Requisitos previos

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)
- [Ollama](https://ollama.ai/) instalado y corriendo

### Descargar los siguientes modelos de Ollama:

```bash
ollama pull deepseek-r1
ollama pull all-minilm
```

### Levantar Elasticsearch:

```bash
docker compose up -d
```

Esto levanta Elasticsearch 8.17 en puesrto 9200

## Cómo ejecutar

```bash
cd ChatRAG
dotnet run
```

La app se levanta en 5001.

## Cómo usar

### 1. Subir documentos

Ir a la página **Upload** (`/upload`) y seleccionar archivos (`.txt`, `.pdf`, `.docx`). La app:

1. Lee el contenido del archivo
2. Lo divide en fragmentos de ~500 tokens (con otros 50 de solapamiento)
3. Genera un embedding (con un vector de 384 dimensiones) para cada fragmento mediante all-minilm
4. Indexa cada fragmento con su vector en Elasticsearch

### 2. Chatear

Andá a **Chat** (`/chat`) y hacé preguntas. El flujo es:

1. Tu pregunta se convierte en embedding
2. Se buscan los 5 fragmentos más similares en Elasticsearch (KNN)
3. El contexto recuperado se inyecta en el system prompt de DeepSeek-R1
4. DeepSeek-R1 responde basándose **exclusivamente** en ese contexto

## Estructura del proyecto

```
ChatRAG/
├── ChatRAG/                     # Proyecto principal
│   ├── Diagram/                 # Diagramas UML del sistema
│   ├── Models/
│   │   ├── ChatMessage.cs       # Modelo de mensaje del chat
│   │   └── TextChunk.cs         # Modelo de fragmento indexado
│   ├── Services/
│   │   ├── Interfaces/          # Contratos (IChatService, IRagService, etc.)
│   │   ├── ChatService.cs       # Orquesta preguntas → RAG → LLM
│   │   ├── RagService.cs        # Pipeline RAG (chunking → embed → index / search)
│   │   ├── EmbeddingService.cs  # POST /api/embed a Ollama
│   │   ├── ElasticsearchService.cs  # CRUD vectorial en ES
│   │   └── TextChunkerService.cs    # Fragmentación de texto (SK TextChunker)
│   ├── Services/
│   │   ├── IDocumentParserService.cs  # Contrato para extraer texto de archivos
│   │   ├── DocumentParserService.cs   # Implementación: .txt (StreamReader), .pdf (PdfPig), .docx (OpenXML)
│   ├── Pages/
│   │   ├── Chat.razor           # Interfaz de chat
│   │   ├── Upload.razor         # Subida de documentos
│   │   └── Index.razor          # Página principal
│   ├── Program.cs               # Punto de entrada y DI
│   └── appsettings.json         # Configuración (Ollama, Elasticsearch)
├── docker-compose.yml           # Elasticsearch en Docker
└── README.md                    # Este archivo
```

## Configuración

Editar `ChatRAG/appsettings.json`:

```json
{
  "Ollama": {
    "Uri": "http://localhost:11434",
    "ChatModel": "deepseek-r1",
    "EmbeddingModel": "all-minilm"
  },
  "Elasticsearch": {
    "Uri": "http://localhost:9200"
  }
}
```

## Diagramas

Los diagramas UML del sistema (clases, secuencia, paquetes, despliegue) están en [`ChatRAG/Diagram/diagramas.md`](ChatRAG/Diagram/diagramas.md) en formato Mermaid.

## Persistencia

| Dato | Dónde | Sobrevive a reinicios |
|---|---|---|
| Fragmentos de documentos + vectores | Elasticsearch (índice `text_chunks`) | Sí |
| Historial de chat | Memoria (`List<ChatMessage>` en `ChatService`) | No |

## Notas

- Formatos aceptados: `.txt`, `.pdf`, `.docx` (máximo 10 archivos por selección, 10 MB por archivo)
- El procesamiento es secuencial (un archivo a la vez)
- Sin autenticación ni multiusuario
- Es una pequeña prueba sin BBDD y sin Logeo para poner en marcha un sistema a modo de presentación.

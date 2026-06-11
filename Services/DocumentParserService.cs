using UglyToad.PdfPig;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace ChatRAG.Services;

public class DocumentParserService : IDocumentParserService
{
    private readonly ILogger<DocumentParserService> _logger;

    public DocumentParserService(ILogger<DocumentParserService> logger)
    {
        _logger = logger;
    }

    public async Task<string> ExtractTextAsync(string fileName, Stream stream)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();

        try
        {
            return ext switch
            {
                ".txt" => await ExtractTxtAsync(stream),
            ".pdf" => await ExtractPdf(stream, fileName),
            ".docx" => await ExtractDocx(stream, fileName),
                _ => throw new NotSupportedException(
                    $"Formato no soportado: '{ext}'. Formatos aceptados: .txt, .pdf, .docx")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al extraer texto de '{FileName}'", fileName);
            throw;
        }
    }

    private static async Task<string> ExtractTxtAsync(Stream stream)
    {
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }

    private static async Task<string> ExtractPdf(Stream stream, string fileName)
    {
        try
        {
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            ms.Position = 0;

            using var pdf = PdfDocument.Open(ms);
            var pages = new List<string>();

            foreach (var page in pdf.GetPages())
            {
                var text = page.Text;
                if (!string.IsNullOrWhiteSpace(text))
                    pages.Add(text);
            }

            return string.Join("\n\n", pages);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"No se pudo leer el PDF '{fileName}': {ex.Message}", ex);
        }
    }

    private static async Task<string> ExtractDocx(Stream stream, string fileName)
    {
        try
        {
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            ms.Position = 0;

            using var doc = WordprocessingDocument.Open(ms, false);
            var body = doc.MainDocumentPart?.Document.Body;
            if (body == null) return string.Empty;

            var paragraphs = body.Elements<Paragraph>()
                .Select(p => p.InnerText)
                .Where(t => !string.IsNullOrWhiteSpace(t));

            return string.Join("\n\n", paragraphs);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"No se pudo leer el DOCX '{fileName}': {ex.Message}", ex);
        }
    }
}

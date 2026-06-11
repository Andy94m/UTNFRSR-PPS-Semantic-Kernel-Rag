namespace ChatRAG.Services;

public interface IDocumentParserService
{
    Task<string> ExtractTextAsync(string fileName, Stream stream);
}

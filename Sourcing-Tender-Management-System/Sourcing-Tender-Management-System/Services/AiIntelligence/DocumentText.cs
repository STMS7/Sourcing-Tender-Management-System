using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace Sourcing_Tender_Management_System.Services.AiIntelligence;

public record TextChunk(int Page, string Text);

public static class DocumentText
{
    public const int ChunkSize = 800;   // characters per piece
    public const int Overlap = 150;     // characters repeated in the next piece

    // 1b goes here
    //read pdf page by page 
    public static List<TextChunk> FromPdf(byte[] pdf)
    {
        using var document = PdfDocument.Open(pdf);
        return document.GetPages().Take(50)
            .SelectMany(page => Split(ContentOrderTextExtractor.GetText(page))
                .Select(text => new TextChunk(page.Number, text)))
            .ToList();
    }
    // 1c goes here
    //Cut the text on whole words
    public static IEnumerable<string> Split(string text)
    {
        var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var start = 0;
        while (start < words.Length)
        {
            // take words until the piece is full
            var end = start;
            var length = 0;
            while (end < words.Length && (end == start || length + words[end].Length + 1 <= ChunkSize))
                length += words[end++].Length + 1;

            yield return string.Join(' ', words[start..end]);
            if (end == words.Length) yield break;

            // step back a few words to overlap, but always move forward
            var back = end;
            for (var kept = 0; back - 1 > start && kept + words[back - 1].Length + 1 <= Overlap; back--)
                kept += words[back - 1].Length + 1;
            start = back;
        }
    }
}
using Sourcing_Tender_Management_System.Models.OfferExtras;

namespace Sourcing_Tender_Management_System.Services.OfferExtras;

/// <summary>
/// Stores sample pictures and guarantee documents outside wwwroot (App_Data/OfferExtras).
/// The real file type is read from the first bytes of the file, never from its name or the browser's claim,
/// and the stored name is a random GUID, so a download always passes through an access check.
/// </summary>
public class OfferExtrasFiles(IWebHostEnvironment environment)
{
    private string Root => Path.Combine(environment.ContentRootPath, "App_Data", "OfferExtras");

    public record Detected(string Extension, string ContentType);

    /// <summary>Returns the detected type, or an error message for the user.</summary>
    public static async Task<(Detected? Type, string? Error)> InspectImageAsync(IFormFile file)
    {
        if (BadName(file) is { } nameError) return (null, nameError);
        if (file.Length < 100 || file.Length > OfferExtrasRules.MaxImageBytes)
            return (null, "Each picture must be between 100 bytes and 3 MB.");
        var head = await ReadHeadAsync(file, 12);
        if (head is null) return (null, "The picture is incomplete.");
        if (head[0] == 0xFF && head[1] == 0xD8 && head[2] == 0xFF) return (new(".jpg", "image/jpeg"), null);
        if (head[0] == 0x89 && head[1] == 0x50 && head[2] == 0x4E && head[3] == 0x47 && head[4] == 0x0D && head[5] == 0x0A && head[6] == 0x1A && head[7] == 0x0A)
            return (new(".png", "image/png"), null);
        if (head[0] == 'R' && head[1] == 'I' && head[2] == 'F' && head[3] == 'F' && head[8] == 'W' && head[9] == 'E' && head[10] == 'B' && head[11] == 'P')
            return (new(".webp", "image/webp"), null);
        return (null, "Only JPG, PNG or WebP pictures are supported.");
    }

    public static async Task<(Detected? Type, string? Error)> InspectDocumentAsync(IFormFile file)
    {
        if (BadName(file) is { } nameError) return (null, nameError);
        if (file.Length < 100 || file.Length > OfferExtrasRules.MaxDocumentBytes)
            return (null, "The guarantee document must be between 100 bytes and 5 MB.");
        var head = await ReadHeadAsync(file, 5);
        if (head is null) return (null, "The document is incomplete.");
        return System.Text.Encoding.ASCII.GetString(head) == "%PDF-"
            ? (new(".pdf", "application/pdf"), null)
            : (null, "The guarantee document must be a PDF file.");
    }

    private static string? BadName(IFormFile file)
    {
        var name = Path.GetFileName(file.FileName);
        return string.IsNullOrWhiteSpace(name) || name.Length > 255 ? "Use a file name of up to 255 characters." : null;
    }

    private static async Task<byte[]?> ReadHeadAsync(IFormFile file, int count)
    {
        var buffer = new byte[count];
        await using var stream = file.OpenReadStream();
        try { await stream.ReadExactlyAsync(buffer); return buffer; }
        catch (EndOfStreamException) { return null; }
    }

    public async Task<string> SaveAsync(IFormFile file, Detected type)
    {
        Directory.CreateDirectory(Root);
        var name = Guid.NewGuid().ToString("N") + type.Extension;
        var path = Path.Combine(Root, name);
        try
        {
            await using var output = new FileStream(path, FileMode.CreateNew);
            await file.CopyToAsync(output);
            return name;
        }
        catch
        {
            if (File.Exists(path)) File.Delete(path);
            throw;
        }
    }

    private static readonly string[] Allowed = [".jpg", ".png", ".webp", ".pdf"];

    public string? Resolve(string name)
    {
        if (name != Path.GetFileName(name) || !Guid.TryParseExact(Path.GetFileNameWithoutExtension(name), "N", out _)
            || !Allowed.Contains(Path.GetExtension(name))) return null;
        var path = Path.Combine(Root, name);
        return File.Exists(path) ? path : null;
    }

    public void Delete(string name)
    {
        var path = Resolve(name);
        if (path is not null) File.Delete(path);
    }
}

namespace Sourcing_Tender_Management_System.Services;

// Store documents outside wwwroot; every download must pass an ownership check.
public class SupplierFileStore(IWebHostEnvironment environment)
{
    private string Root => Path.Combine(environment.ContentRootPath, "App_Data", "SupplierFiles");
    public async Task<string?> ValidateAsync(IFormFile file)
    {
        var originalName = Path.GetFileName(file.FileName);
        if (string.IsNullOrWhiteSpace(originalName) || originalName.Length > 255) return "Use a document filename of up to 255 characters.";
        if (file.Length is < 5 or > 5 * 1024 * 1024) return "Each PDF must be between 5 bytes and 5 MB.";
        if (!string.Equals(Path.GetExtension(file.FileName), ".pdf", StringComparison.OrdinalIgnoreCase))
            return "Only PDF documents are supported.";
        await using var input = file.OpenReadStream();
        var header = new byte[5];
        try { await input.ReadExactlyAsync(header); }
        catch (EndOfStreamException) { return "The document is incomplete."; }
        return System.Text.Encoding.ASCII.GetString(header) == "%PDF-" ? null : "The document is not a valid PDF file.";
    }
    public async Task<string> SaveAsync(IFormFile file)
    {
        Directory.CreateDirectory(Root);
        var name = Guid.NewGuid().ToString("N") + ".pdf";
        var path = Path.Combine(Root, name);
        try
        {
            await using var output = new FileStream(path, FileMode.CreateNew);
            await file.CopyToAsync(output);
            return name;
        }
        catch
        {
            // Do not leave a partial upload behind if writing fails.
            if (File.Exists(path)) File.Delete(path);
            throw;
        }
    }
    public string? Resolve(string name)
    {
        if (name != Path.GetFileName(name) || !Guid.TryParseExact(Path.GetFileNameWithoutExtension(name), "N", out _)
            || Path.GetExtension(name) != ".pdf") return null;
        var path = Path.Combine(Root, name);
        return File.Exists(path) ? path : null;
    }
    public void Delete(string name)
    {
        var path = Resolve(name);
        if (path is not null) File.Delete(path);
    }
}

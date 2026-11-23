using System.Buffers.Binary;

namespace Sourcing_Tender_Management_System.Services.SupplierBidExtras;

// Private storage: callers must check bid ownership before resolving a key.
public class SampleImageStore(IWebHostEnvironment environment)
{
    public const int MaxImages = 5;
    public const int MaxFileBytes = 5 * 1024 * 1024;
    private string Root => Path.Combine(environment.ContentRootPath, "App_Data", "BidSampleImages");

    public string? ValidateCount(int count) => count is < 0 or > MaxImages
        ? $"Attach at most {MaxImages} sample images." : null;

    public async Task<string?> ValidateAsync(IFormFile file)
    {
        var name = Path.GetFileName(file.FileName);
        if (string.IsNullOrWhiteSpace(name) || name.Length > 255)
            return "Use an image filename of up to 255 characters.";
        if (file.Length is < 24 or > MaxFileBytes)
            return "Each sample image must be a JPEG or PNG of up to 5 MB.";
        var extension = Path.GetExtension(name).ToLowerInvariant();
        if (extension is not (".png" or ".jpg" or ".jpeg"))
            return "Sample images must be JPEG or PNG files.";
        await using var input = file.OpenReadStream();
        var bytes = new byte[(int)file.Length];
        try { await input.ReadExactlyAsync(bytes); }
        catch (EndOfStreamException) { return "The sample image is incomplete."; }
        var valid = extension == ".png" ? IsPng(bytes) : IsJpeg(bytes);
        return valid ? null : "The sample image content does not match a supported JPEG or PNG file.";
    }

    private static bool IsPng(ReadOnlySpan<byte> bytes)
    {
        if (!bytes[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) return false;
        var offset = 8;
        var hasData = false;
        while (offset <= bytes.Length - 12)
        {
            var length = BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(offset, 4));
            if (length > (uint)(bytes.Length - offset - 12)) return false;
            var type = bytes.Slice(offset + 4, 4);
            if (offset == 8 && (length != 13 || !type.SequenceEqual("IHDR"u8) ||
                BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(offset + 8, 4)) == 0 ||
                BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(offset + 12, 4)) == 0)) return false;
            if (type.SequenceEqual("IDAT"u8)) hasData = true;
            if (type.SequenceEqual("IEND"u8)) return hasData && length == 0 && offset + 12 == bytes.Length;
            offset += (int)length + 12;
        }
        return false;
    }

    private static bool IsJpeg(ReadOnlySpan<byte> bytes)
    {
        if (bytes[0] != 0xff || bytes[1] != 0xd8 || bytes[^2] != 0xff || bytes[^1] != 0xd9) return false;
        var offset = 2;
        var hasFrame = false;
        while (offset < bytes.Length - 2)
        {
            if (bytes[offset++] != 0xff) return false;
            while (offset < bytes.Length && bytes[offset] == 0xff) offset++;
            if (offset >= bytes.Length - 2) return false;
            var marker = bytes[offset++];
            if (marker is 0x00 or 0xd8 or 0xd9 || marker is >= 0xd0 and <= 0xd7) return false;
            if (offset > bytes.Length - 2) return false;
            var length = BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(offset, 2));
            if (length < 2 || length > bytes.Length - offset) return false;
            if (marker is 0xc0 or 0xc1 or 0xc2)
            {
                if (length < 8 || BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(offset + 3, 2)) == 0 ||
                    BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(offset + 5, 2)) == 0) return false;
                hasFrame = true;
            }
            if (marker == 0xda) return hasFrame && offset + length < bytes.Length - 2;
            offset += length;
        }
        return false;
    }

    public async Task<string> SaveAsync(IFormFile file)
    {
        if (await ValidateAsync(file) is string error) throw new InvalidDataException(error);
        Directory.CreateDirectory(Root);
        var extension = Path.GetExtension(file.FileName).Equals(".png", StringComparison.OrdinalIgnoreCase) ? ".png" : ".jpg";
        var key = Guid.NewGuid().ToString("N") + extension;
        var path = Path.Combine(Root, key);
        try
        {
            await using var output = new FileStream(path, FileMode.CreateNew);
            await file.CopyToAsync(output);
            return key;
        }
        catch
        {
            if (File.Exists(path)) File.Delete(path);
            throw;
        }
    }

    public string? Resolve(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || key != Path.GetFileName(key) ||
            !Guid.TryParseExact(Path.GetFileNameWithoutExtension(key), "N", out _) ||
            Path.GetExtension(key) is not (".png" or ".jpg")) return null;
        var path = Path.Combine(Root, key);
        return File.Exists(path) ? path : null;
    }

    public void Delete(string key)
    {
        var path = Resolve(key);
        if (path is not null) File.Delete(path);
    }
}

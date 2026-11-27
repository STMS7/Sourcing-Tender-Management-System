using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Sourcing_Tender_Management_System.Data;
using Sourcing_Tender_Management_System.Services.Reports;

namespace Sourcing_Tender_Management_System.Services.AiIntelligence;


public record DocumentHit(string File, string Supplier, int Page, string Text, double Score);


//Checks the role, then returns the 4 closest pieces.
public class DocumentSearch(ApplicationDbContext db, EmbeddingClient embeddings, SupplierFileStore files, IWebHostEnvironment environment)
{
    // Kept in memory so each file is read only once while the app runs.
    private static readonly ConcurrentDictionary<string, List<(TextChunk Chunk, float[] Vector)>> Index = new();

    // Files the admin uploads on the AI page.
    public static string KnowledgeFolder(IWebHostEnvironment env) => Path.Combine(env.ContentRootPath, "App_Data", "KnowledgeFiles");

    // Stored as "<32-character id>_<original name>"; this gives the original name back.
    public static string DisplayName(string path)
    {
        var name = Path.GetFileName(path);
        return name.Length > 33 && name[32] == '_' ? name[33..] : name;
    }

    public async Task<List<DocumentHit>> SearchAsync(ClaimsPrincipal user, string question, CancellationToken ct)
    {
        // Every staff role may ask about the files the admin uploads. Suppliers may not.
        if (!ReportAccess.AllRoles.Any(user.IsInRole)) throw new UnauthorizedAccessException();

        var query = (await embeddings.EmbedAsync(["search_query: " + question], ct))[0];
        // Supplier registration documents keep the admin pages' rule: only Admin may read them.
        var isAdmin = user.IsInRole("Admin");
        var documents = await db.SupplierDocuments.AsNoTracking().Where(d => isAdmin)
            .Select(d => new { d.Id, d.FileName, d.FilePath, Supplier = d.Supplier.CompanyName }).ToListAsync(ct);

        var hits = new List<DocumentHit>();
        var sources = documents.Select(d => (File: d.FileName, Owner: d.Supplier, Path: files.Resolve(d.FilePath))).ToList();
        var folder = KnowledgeFolder(environment);
        if (Directory.Exists(folder))
            sources.AddRange(Directory.GetFiles(folder, "*.pdf").Select(f => (File: DisplayName(f), Owner: "Uploaded by admin", Path: (string?)f)));

        foreach (var (file, owner, path) in sources)
        {
            if (path is null) continue;
            if (!Index.TryGetValue(path, out var pieces))
            {
                var chunks = DocumentText.FromPdf(await File.ReadAllBytesAsync(path, ct));
                var vectors = chunks.Count == 0 ? []
                    : await embeddings.EmbedAsync(chunks.Select(x => "search_document: " + x.Text).ToList(), ct);
                Index[path] = pieces = chunks.Zip(vectors).ToList();
            }
            hits.AddRange(pieces.Select(x => new DocumentHit(file, owner, x.Chunk.Page, x.Chunk.Text, Similarity(query, x.Vector))));
        }
        return hits.OrderByDescending(h => h.Score).Take(4).ToList();
    }

    // Cosine similarity: 1 means same direction (same meaning), 0 means unrelated.
    public static double Similarity(float[] a, float[] b)
    {
        double dot = 0, lengthA = 0, lengthB = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            lengthA += a[i] * a[i];
            lengthB += b[i] * b[i];
        }
        return lengthA == 0 || lengthB == 0 ? 0 : dot / Math.Sqrt(lengthA * lengthB);
    }
}
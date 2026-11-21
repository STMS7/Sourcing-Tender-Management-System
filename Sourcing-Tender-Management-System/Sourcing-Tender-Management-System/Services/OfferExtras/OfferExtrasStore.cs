using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Sourcing_Tender_Management_System.Data;
using Sourcing_Tender_Management_System.Models;
using Sourcing_Tender_Management_System.Models.OfferExtras;

namespace Sourcing_Tender_Management_System.Services.OfferExtras;

/// <summary>
/// Reads and writes the two tables of this feature (offerx.BidSamples and offerx.BidGuarantees).
/// They are not part of the shared EF model, so every statement is plain parameterised SQL.
/// Queries are always materialised with ToListAsync: EF must not wrap them in a sub-query.
/// </summary>
public class OfferExtrasStore(ApplicationDbContext db)
{
    public async Task<OfferExtrasPanel> LoadAsync(Bid bid, bool canEdit, bool supplierView) => new()
    {
        BidId = bid.Id, Currency = bid.Currency, CanEdit = canEdit, SupplierView = supplierView,
        Deadline = bid.Tender.SubmissionDeadline,
        Samples = await SamplesAsync(bid.Id), Guarantees = await GuaranteesAsync(bid.Id)
    };

    public Task<List<BidSampleRow>> SamplesAsync(int bidId) => db.Database.SqlQuery<BidSampleRow>($"""
        SELECT [Id],[BidId],[StoredName],[OriginalName],[ContentType],[SizeBytes],[Caption],[UploadedAt]
        FROM [offerx].[BidSamples] WHERE [BidId] = {bidId} ORDER BY [Id]
        """).ToListAsync();

    public async Task<BidSampleRow?> SampleAsync(long id) => (await db.Database.SqlQuery<BidSampleRow>($"""
        SELECT [Id],[BidId],[StoredName],[OriginalName],[ContentType],[SizeBytes],[Caption],[UploadedAt]
        FROM [offerx].[BidSamples] WHERE [Id] = {id}
        """).ToListAsync()).FirstOrDefault();

    public async Task<int> CountSamplesAsync(int bidId) => (await db.Database.SqlQuery<int>($"""
        SELECT COUNT(*) AS [Value] FROM [offerx].[BidSamples] WHERE [BidId] = {bidId}
        """).ToListAsync()).Single();

    public Task AddSampleAsync(int bidId, string storedName, string originalName, string contentType, long size, string? caption) =>
        db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO [offerx].[BidSamples] ([BidId],[StoredName],[OriginalName],[ContentType],[SizeBytes],[Caption],[UploadedAt])
            VALUES ({bidId},{storedName},{originalName},{contentType},{size},{caption},{DateTime.UtcNow})
            """);

    public Task DeleteSampleAsync(long id) =>
        db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM [offerx].[BidSamples] WHERE [Id] = {id}");

    public Task<List<GuaranteeRow>> GuaranteesAsync(int bidId) => db.Database.SqlQuery<GuaranteeRow>($"""
        SELECT [Id],[BidId],[Kind],[Purpose],[GuaranteeNumber],[IssuerName],[Amount],[Currency],[DurationMonths],
               [IssuedOn],[ExpiresOn],[StoredName],[OriginalName],[VerifyCode],[VerifyCount],[LastVerifiedAt],[CreatedAt]
        FROM [offerx].[BidGuarantees] WHERE [BidId] = {bidId} ORDER BY [Id]
        """).ToListAsync();

    public async Task<GuaranteeRow?> GuaranteeAsync(long id) => (await db.Database.SqlQuery<GuaranteeRow>($"""
        SELECT [Id],[BidId],[Kind],[Purpose],[GuaranteeNumber],[IssuerName],[Amount],[Currency],[DurationMonths],
               [IssuedOn],[ExpiresOn],[StoredName],[OriginalName],[VerifyCode],[VerifyCount],[LastVerifiedAt],[CreatedAt]
        FROM [offerx].[BidGuarantees] WHERE [Id] = {id}
        """).ToListAsync()).FirstOrDefault();

    public async Task<GuaranteeRow?> GuaranteeByNumberAsync(string number) => (await db.Database.SqlQuery<GuaranteeRow>($"""
        SELECT [Id],[BidId],[Kind],[Purpose],[GuaranteeNumber],[IssuerName],[Amount],[Currency],[DurationMonths],
               [IssuedOn],[ExpiresOn],[StoredName],[OriginalName],[VerifyCode],[VerifyCount],[LastVerifiedAt],[CreatedAt]
        FROM [offerx].[BidGuarantees] WHERE [GuaranteeNumber] = {number}
        """).ToListAsync()).FirstOrDefault();

    public async Task<int> CountGuaranteesAsync(int bidId) => (await db.Database.SqlQuery<int>($"""
        SELECT COUNT(*) AS [Value] FROM [offerx].[BidGuarantees] WHERE [BidId] = {bidId}
        """).ToListAsync()).Single();

    /// <summary>Returns false when the guarantee number is already registered on any offer.</summary>
    public async Task<bool> AddGuaranteeAsync(GuaranteeRow g)
    {
        try
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO [offerx].[BidGuarantees]
                    ([BidId],[Kind],[Purpose],[GuaranteeNumber],[IssuerName],[Amount],[Currency],[DurationMonths],
                     [IssuedOn],[ExpiresOn],[StoredName],[OriginalName],[VerifyCode],[CreatedAt])
                VALUES ({g.BidId},{g.Kind},{g.Purpose},{g.GuaranteeNumber},{g.IssuerName},{g.Amount},{g.Currency},{g.DurationMonths},
                        {g.IssuedOn.Date},{g.ExpiresOn.Date},{g.StoredName},{g.OriginalName},{g.VerifyCode},{DateTime.UtcNow})
                """);
            return true;
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 }) { return false; }
        catch (SqlException ex) when (ex.Number is 2601 or 2627) { return false; }
    }

    public Task DeleteGuaranteeAsync(long id) =>
        db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM [offerx].[BidGuarantees] WHERE [Id] = {id}");

    public Task RecordCheckAsync(long id) => db.Database.ExecuteSqlInterpolatedAsync($"""
        UPDATE [offerx].[BidGuarantees] SET [VerifyCount] = [VerifyCount] + 1, [LastVerifiedAt] = {DateTime.UtcNow} WHERE [Id] = {id}
        """);
}

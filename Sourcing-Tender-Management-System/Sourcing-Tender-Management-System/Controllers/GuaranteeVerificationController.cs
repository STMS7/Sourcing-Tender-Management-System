using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using QRCoder;
using Sourcing_Tender_Management_System.Data;
using Sourcing_Tender_Management_System.Models.OfferExtras;
using Sourcing_Tender_Management_System.Services.OfferExtras;

namespace Sourcing_Tender_Management_System.Controllers;

/// <summary>
/// A stand-alone page that plays the part of the issuing bank's guarantee verification portal (a simulation for this
/// system). Anyone holding the guarantee number and the verification code printed with it - the QR code carries both -
/// can check the guarantee. A wrong number and a wrong code give the same answer, and repeated failures are slowed down.
/// </summary>
[AllowAnonymous]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class GuaranteeVerificationController(ApplicationDbContext db, OfferExtrasStore store, IMemoryCache cache) : Controller
{
    private const int MaxFailures = 10;

    public static string VerifyUrl(IUrlHelper url, HttpRequest request, GuaranteeRow g) =>
        url.Action("Index", "GuaranteeVerification", new { number = g.GuaranteeNumber, code = g.VerifyCode },
            request.Scheme, request.Host.Value)!;

    public static byte[] QrPng(string text)
    {
        using var data = QRCodeGenerator.GenerateQrCode(text, QRCodeGenerator.ECCLevel.Q);
        using var renderer = new PngByteQRCode(data);
        return renderer.GetGraphic(8);
    }

    private string FailureKey => "gv-fail:" + (HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");

    private bool Throttled => cache.TryGetValue(FailureKey, out int fails) && fails >= MaxFailures;

    private void CountFailure()
    {
        cache.TryGetValue(FailureKey, out int fails);
        cache.Set(FailureKey, fails + 1, TimeSpan.FromMinutes(10));
    }

    private async Task<GuaranteeRow?> MatchAsync(string? number, string? code)
    {
        number = number?.Trim().ToUpperInvariant();
        var wanted = OfferExtrasRules.NormalizeCode(code);
        if (string.IsNullOrEmpty(number) || number.Length > 40 || wanted.Length != 12) return null;
        var row = await store.GuaranteeByNumberAsync(number);
        if (row is null) return null;
        var actual = Encoding.UTF8.GetBytes(OfferExtrasRules.NormalizeCode(row.VerifyCode));
        return CryptographicOperations.FixedTimeEquals(actual, Encoding.UTF8.GetBytes(wanted)) ? row : null;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? number, string? code)
    {
        var model = new GuaranteeCheckViewModel { Number = number?.Trim(), Code = code?.Trim() };
        if (string.IsNullOrWhiteSpace(number) && string.IsNullOrWhiteSpace(code)) return View(model);

        model.Searched = true;
        if (Throttled) { model.Throttled = true; return View(model); }

        var row = await MatchAsync(number, code);
        if (row is null) { CountFailure(); return View(model); }

        model.Found = row;
        var today = DateTime.UtcNow.Date;
        model.Expired = row.IsExpired(today);
        model.DaysLeft = Math.Max(0, (row.ExpiresOn.Date - today).Days);
        var info = await db.Bids.AsNoTracking().Where(x => x.Id == row.BidId)
            .Select(x => new { x.Tender.TenderNumber, Supplier = x.Supplier.CompanyName }).SingleOrDefaultAsync();
        model.TenderNumber = info?.TenderNumber;
        model.SupplierName = info?.Supplier;
        await store.RecordCheckAsync(row.Id);
        return View(model);
    }

    /// <summary>The QR picture. It only draws for a number and code that match, so it cannot be used to guess either.</summary>
    [HttpGet]
    public async Task<IActionResult> Qr(string? number, string? code)
    {
        if (Throttled) return NotFound();
        var row = await MatchAsync(number, code);
        if (row is null) { CountFailure(); return NotFound(); }
        return File(QrPng(VerifyUrl(Url, Request, row)), "image/png");
    }
}

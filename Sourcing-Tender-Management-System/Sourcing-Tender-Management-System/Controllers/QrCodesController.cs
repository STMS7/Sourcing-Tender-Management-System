using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using Sourcing_Tender_Management_System.Data;
using Sourcing_Tender_Management_System.Services;

namespace Sourcing_Tender_Management_System.Controllers;

// Separate read-only feature. Reuse the officer workspace's record access policy.
[Authorize(Roles = "ProcurementOfficer,SourcingOfficer")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class QrCodesController(ApplicationDbContext db) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Tender(int id, bool download = false)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Forbid();
        if (!await SourcingCommunicationPolicy.TendersFor(db, userId).AsNoTracking().AnyAsync(x => x.Id == id))
            return NotFound();
        return ImageFor("Details", id, "tender", download);
    }

    [HttpGet]
    public async Task<IActionResult> PurchaseRequest(int id, bool download = false)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Forbid();
        if (!await SourcingCommunicationPolicy.RequestsForCommunication(db, userId).AsNoTracking().AnyAsync(x => x.Id == id))
            return NotFound();
        return ImageFor("RequestDetails", id, "purchase-request", download);
    }

    private IActionResult ImageFor(string action, int id, string prefix, bool download)
    {
        // Url.Action includes the current scheme, host and application path base.
        var target = Url.Action(action, "SourcingOfficer", new { id }, Request.Scheme, Request.Host.Value);
        if (target is null) return Problem("The detail-page URL could not be generated.");
        using var data = QRCodeGenerator.GenerateQrCode(target, QRCodeGenerator.ECCLevel.Q);
        using var renderer = new PngByteQRCode(data);
        var png = renderer.GetGraphic(8);
        return download ? File(png, "image/png", $"{prefix}-{id}-qr.png") : File(png, "image/png");
    }
}

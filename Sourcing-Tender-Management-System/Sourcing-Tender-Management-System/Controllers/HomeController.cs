using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sourcing_Tender_Management_System.Data;
using Sourcing_Tender_Management_System.Models;
using System.Diagnostics;

namespace Sourcing_Tender_Management_System.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _db;

        public HomeController(ApplicationDbContext db)
        {
            _db = db;
        }

        // Public landing page (Zaytouna). Statistics come from the real database.
        public async Task<IActionResult> Index()
        {
            ViewBag.ActiveTenders = await _db.Tenders.CountAsync(t => t.Status == TenderStatus.Published);
            ViewBag.ApprovedSuppliers = await _db.Suppliers.CountAsync(s => s.IsActive && s.RegistrationStatus == SupplierRegistrationStatus.Approved);
            ViewBag.CompletedRequests = await _db.ProcurementRequests.CountAsync(r => r.Status == ProcurementRequestStatus.ConvertedToTender);
            ViewBag.BidsReceived = await _db.Bids.CountAsync(b => b.Status != BidStatus.Draft);
            return View();
        }

        // Contact form on the landing page.
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Contact(string name, string email, string? subject, string message)
        {
            name = (name ?? string.Empty).Trim();
            email = (email ?? string.Empty).Trim();
            message = (message ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(message) ||
                !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email))
            {
                TempData["ContactStatus"] = "Please fill in your name, a valid email and a message.";
                return Redirect(Url.Action(nameof(Index)) + "#contact");
            }

            var body = string.IsNullOrWhiteSpace(subject) ? message : $"[{subject.Trim()}] {message}";
            _db.ContactMessages.Add(new ContactMessage
            {
                Name = name.Length > 150 ? name[..150] : name,
                Email = email.Length > 256 ? email[..256] : email,
                Message = body.Length > 2000 ? body[..2000] : body
            });
            await _db.SaveChangesAsync();

            TempData["ContactStatus"] = "Thank you. Your message has been sent.";
            return Redirect(Url.Action(nameof(Index)) + "#contact");
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}

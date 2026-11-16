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
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public async Task<IActionResult> Index()
        {
            // Home is the public boundary requested by the team: leaving the
            // workspace ends the session. A GET only presents the existing,
            // antiforgery-protected logout POST; it does not mutate authentication.
            if (User.Identity?.IsAuthenticated == true) return View("LeaveWorkspace");
            ViewBag.ActiveTenders = await _db.Tenders.CountAsync(t => t.Status == TenderStatus.Published);
            ViewBag.ApprovedSuppliers = await _db.Suppliers.CountAsync(s => s.IsActive && s.RegistrationStatus == SupplierRegistrationStatus.Approved);
            ViewBag.CompletedRequests = await _db.ProcurementRequests.CountAsync(r => r.Status == ProcurementRequestStatus.ConvertedToTender);
            ViewBag.BidsReceived = await _db.Bids.CountAsync(b => b.Status != BidStatus.Draft);
            return View();
        }

        // About us page.
        public IActionResult About()
        {
            return View();
        }

        // Contact us page.
        [HttpGet]
        public IActionResult Contact()
        {
            return View();
        }

        // Contact form (posted from the Contact us page).
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
                return Redirect(Url.Action(nameof(Contact)) + "#contact");
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
            return Redirect(Url.Action(nameof(Contact)) + "#contact");
        }

        // Public page introducing the team behind the platform.
        [HttpGet]
        public IActionResult PoweredBy()
        {
            return View(PoweredByViewModel.Create());
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
